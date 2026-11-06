using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace XlsxInspector
{
    public partial class SpreadsheetInspector
    {
        private const float AddHeaderSize = 24; // 表头最右侧和最下侧为插入按钮保留的固定空间。
        private int _dataRows; // 实际内容边界，独立于预览和追加的空白行数。
        private int _dataColumns; // 实际内容边界，独立于预览和追加的空白列数。
        private Dictionary<int, float> _excelRowHeights; // 已定义的 Excel 行高，空白预览行使用默认值。
        private Dictionary<int, float> _excelColumnWidths; // 已定义的 Excel 列宽，不为预览创建 Excel 列。
        private float _excelDefaultHeight; // 工作表默认行高。
        private float _excelDefaultWidth; // 工作表默认列宽。

        // 网格坐标使用前缀和：第 c 列的左边界为 columnEdges[c]，宽度为相邻边界之差。
        // 滚动后仅遍历可见行列，固定表头在滚动区域外单独绘制。
        private void RebuildGrid()
        {
            _dataRows = _document.ContentRowCount;
            _dataColumns = _document.ContentColumnCount;
            if (!_document.IsCsv)
            {
                (_excelDefaultHeight, _excelDefaultWidth, _excelRowHeights, _excelColumnWidths) = _document.ReadDimensions();
            }
            _rows = Math.Min(_document.MaxRows, Math.Max(_rows, Math.Max(SpreadsheetSettings.instance.PreviewRows, _dataRows + 1)));
            _columns = Math.Min(_document.MaxColumns, Math.Max(_columns, Math.Max(SpreadsheetSettings.instance.PreviewColumns, _dataColumns + 1)));
            // 选区保留文档坐标，即使其所在的空白预览暂时被释放，也不能擅自改变活动单元格。
            _selected = new Vector2Int(Mathf.Clamp(_selected.x, 0, _document.MaxColumns - 1), Mathf.Clamp(_selected.y, 0, _document.MaxRows - 1));
            _anchor = new Vector2Int(Mathf.Clamp(_anchor.x, 0, _document.MaxColumns - 1), Mathf.Clamp(_anchor.y, 0, _document.MaxRows - 1));
            _columnEdges = new float[_columns + 1];
            _rowEdges = new float[_rows + 1];
            for (int column = 0; column < _columns; column++)
            {
                _columnEdges[column + 1] = _columnEdges[column] + ColumnWidth(column);
            }
            for (int row = 0; row < _rows; row++)
            {
                _rowEdges[row + 1] = _rowEdges[row] + RowHeight(row);
            }
        }


        private float ColumnWidth(int column)
        {
            float width = _document.IsCsv ? (column < _csvLayout.columnWidths.Length ? _csvLayout.columnWidths[column] : 0)
                : _excelColumnWidths.TryGetValue(column, out float defined) ? defined : _excelDefaultWidth;
            return Mathf.Clamp(width > 0 ? width : SpreadsheetSettings.instance.ColumnWidth, MinColumnWidth, MaxColumnWidth);
        }


        private float RowHeight(int row)
        {
            float height = _document.IsCsv ? (row < _csvLayout.rowHeights.Length ? _csvLayout.rowHeights[row] : 0)
                : _excelRowHeights.TryGetValue(row, out float defined) ? defined : _excelDefaultHeight;
            return Mathf.Clamp(height > 0 ? height : SpreadsheetSettings.instance.RowHeight, MinRowHeight, MaxRowHeight);
        }


        private void DrawGrid(Rect area)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _gridRect = area;
            }
            var body = new Rect(area.x + RowHeaderWidth, area.y + HeaderHeight,
                area.width - RowHeaderWidth, area.height - HeaderHeight);
            if (Event.current.type != EventType.Layout)
            {
                // Layout 的 GUILayout 矩形尚未求解，不能用它覆盖实际视口或改变预览范围。
                _viewportSize = body.size - new Vector2(16, 16);
                EnsurePreviewAt(_scroll + _viewportSize);
            }
            int gridControl = GUIUtility.GetControlID(FocusType.Keyboard);
            int resizeControl = GUIUtility.GetControlID(FocusType.Passive);

            EditorGUI.DrawRect(area, GridLine);
            // 此处只登记原生滚动条和滚轮状态；单元格稍后通过 Group 使用同一滚动位置绘制。
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0, 0, _columnEdges[_columns], _rowEdges[_rows]));
            GUI.EndScrollView();
            // 滚动控件先登记，点击、拖选和调整尺寸随后处理，不让消费事件改变滚动控件的 ID。
            HandleResize(area, resizeControl);
            HandleGridMouse(new Rect(body.position, _viewportSize), gridControl);
            // 滚轮或滚动条可能在本次 BeginScrollView 中改变位置；补出下一屏，不让用户撞到预览边界。
            if (Event.current.type != EventType.Layout)
            {
                EnsurePreviewAt(_scroll + _viewportSize);
                if (Event.current.type == EventType.Repaint && GUIUtility.hotControl == 0)
                {
                    // BeginScrollView 可能应用上一轮滚轮事件的位置；取得新位置后才能决定可回收范围。
                    ReleaseUnusedPreview();
                }
            }
        }


        // 先创建固定数量的滚动控件，再绘制搜索输入，最后才绘制数量随视口变化的单元格。
        // 两层 Group 只负责裁切和坐标平移，不再创建第二套滚动条或滚动状态。
        private void DrawGridCells(Rect area)
        {
            var viewport = new Rect(area.x + RowHeaderWidth, area.y + HeaderHeight, _viewportSize.x, _viewportSize.y);
            var body = new Rect(viewport.position, viewport.size);
            Color background = GridBackground;
            Color stripe = _palette.Alternate;
            int tableRows = _dataRows;
            int tableColumns = _dataColumns;
            GUI.BeginGroup(viewport);
            GUI.BeginGroup(new Rect(-_scroll.x, -_scroll.y, _columnEdges[_columns], _rowEdges[_rows]));
            int firstRow = FindIndex(_rowEdges, _scroll.y);
            int firstColumn = FindIndex(_columnEdges, _scroll.x);
            for (int row = firstRow; row < _rows && _rowEdges[row] < _scroll.y + body.height; row++)
            {
                for (int column = firstColumn; column < _columns && _columnEdges[column] < _scroll.x + body.width; column++)
                {
                    var rect = new Rect(_columnEdges[column], _rowEdges[row],
                        _columnEdges[column + 1] - _columnEdges[column] - 1, _rowEdges[row + 1] - _rowEdges[row] - 1);
                    bool selected = row >= MinRow && row <= MaxRow && column >= MinColumn && column <= MaxColumn;
                    // 亮色边界只取实际内容，追加空白不扩大它；清空边缘内容后会自动缩回。
                    bool inTable = row < tableRows && column < tableColumns;
                    EditorGUI.DrawRect(rect, selected ? SelectionBackground
                        : !inTable ? EmptyBackground : SpreadsheetSettings.instance.AlternateRows && row % 2 == 0 ? stripe : background);
                    if (row == _selected.y && column == _selected.x)
                    {
                        DrawActiveCellOutline(rect);
                    }
                    if (_editingInline && row == _selected.y && column == _selected.x)
                    {
                        DrawInlineEditor(rect);
                        continue;
                    }

                    string value = row == _selected.y && column == _selected.x && _editChanged
                        ? _editBuffer : _document.GetText(row, column);
                    CellFormat format = GetDisplayFormat(row, column);
                    _cellStyle.alignment = GetTextAnchor(row, column, format);
                    _cellStyle.wordWrap = format.WrapText;
                    if (_searchCells.Contains(new Vector2Int(column, row)))
                    {
                        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), _palette.Search);
                    }
                    string tooltip = SpreadsheetSettings.instance.CellTooltips
                        ? Address(row, column) + (value.Length > 0 ? "\n" + value : "\nEmpty cell")
                            + "\n\nDouble-click or F2 to edit. Right-click for cell actions." : "";
                    GUI.Label(new Rect(rect.x + 4, rect.y + 1, rect.width - 8, rect.height - 2), new GUIContent(value, tooltip), _cellStyle);
                }
            }
            // 内容区的右边和下边保留可辨识的边界，空表时两个长度均为 0。
            EditorGUI.DrawRect(new Rect(_columnEdges[tableColumns] - 1, 0, 1, _rowEdges[tableRows]), Accent);
            EditorGUI.DrawRect(new Rect(0, _rowEdges[tableRows] - 1, _columnEdges[tableColumns], 1), Accent);
            GUI.EndGroup();
            GUI.EndGroup();
        }


        private void DrawInlineEditor(Rect rect)
        {
            GUI.SetNextControlName(InlineControl);
            EditorGUI.BeginChangeCheck();
            _editBuffer = GUI.TextArea(rect, _editBuffer, _inlineStyle);
            if (EditorGUI.EndChangeCheck())
            {
                _editChanged = true;
            }
            if (_focusInline && Event.current.type == EventType.Repaint)
            {
                // 先让新建 TextArea 完成注册，再设置焦点；双击事件当帧可能还没有有效的文本控件。
                GUI.FocusControl(InlineControl);
                _focusInline = false;
            }
        }


        private void DrawHeaders(Rect area)
        {
            // 行号与列字母交叉的左上角是全选按钮，大小与两侧固定表头一致。
            var corner = new Rect(area.x, area.y, RowHeaderWidth - 1, HeaderHeight - 1);
            if (DrawHeaderButton(corner, "◩", true, false, "Select the content range (Ctrl/Cmd+A), including empty cells inside it. Empty trailing cells are excluded."))
            {
                SelectAll();
            }
            // 加号固定在表头外侧，在选区之后插入并移动原有数据；空白预览自身不需要追加操作。
            DrawInsertButton(new Rect(area.xMax - AddHeaderSize, area.y, AddHeaderSize - 1, HeaderHeight - 1), false);
            DrawInsertButton(new Rect(area.x, area.yMax - AddHeaderSize, RowHeaderWidth - 1, AddHeaderSize - 1), true);
            GUI.BeginGroup(new Rect(area.x + RowHeaderWidth, area.y, area.width - RowHeaderWidth - AddHeaderSize, HeaderHeight));
            for (int column = FindIndex(_columnEdges, _scroll.x); column < _columns; column++)
            {
                float x = _columnEdges[column] - _scroll.x;
                if (x > area.width)
                {
                    break;
                }
                var rect = new Rect(x, 0, ColumnWidth(column) - 1, HeaderHeight - 1);
                string label = Address(0, column).TrimEnd('1');
                if (DrawHeaderButton(rect, label, column < _dataColumns, column >= MinColumn && column <= MaxColumn,
                    $"Column {label}: click to select. Drag the right edge to resize; double-click the edge to Auto Fit.\n" +
                    (column < _dataColumns ? "Within the content range." : "Empty area: type or paste to extend the content range.")))
                {
                    SelectRange(new Vector2Int(column, 0), new Vector2Int(column, _document.RowCount - 1));
                }
            }
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(area.x, area.y + HeaderHeight, RowHeaderWidth, area.height - HeaderHeight - AddHeaderSize));
            for (int row = FindIndex(_rowEdges, _scroll.y); row < _rows; row++)
            {
                float y = _rowEdges[row] - _scroll.y;
                if (y > area.height)
                {
                    break;
                }
                var rect = new Rect(0, y, RowHeaderWidth - 1, RowHeight(row) - 1);
                if (DrawHeaderButton(rect, (row + 1).ToString(), row < _dataRows, row >= MinRow && row <= MaxRow,
                    $"Row {row + 1}: click to select. Drag the bottom edge to resize; double-click the edge to Auto Fit.\n" +
                    (row < _dataRows ? "Within the content range." : "Empty area: type or paste to extend the content range.")))
                {
                    SelectRange(new Vector2Int(0, row), new Vector2Int(_document.ColumnCount - 1, row));
                }
            }
            GUI.EndGroup();
        }


        // 拖动表头边界调整尺寸。hotControl 让鼠标离开边界后仍继续把拖动事件交给本控件。
        private void HandleResize(Rect area, int control)
        {
            if (!_writable)
            {
                return;
            }

            Event current = Event.current;
            if (_resizeIndex >= 0 && GUIUtility.hotControl == control)
            {
                if (current.type == EventType.MouseDrag)
                {
                    float pointer = _resizeColumn ? current.mousePosition.x : current.mousePosition.y;
                    float size = _resizeOriginalSize + pointer - _resizePointer;
                    Run(() =>
                    {
                        if (!_resizeHasHistory)
                        {
                            if (Mathf.Approximately(size, _resizeOriginalSize))
                            {
                                return;
                            }
                            BeginHistory(_resizeColumn ? "Resize spreadsheet columns" : "Resize spreadsheet rows", !_document.IsCsv);
                            _resizeHasHistory = true;
                        }
                        for (int index = _resizeFirst; index <= _resizeLast; index++)
                        {
                            if (_resizeColumn)
                            {
                                SetColumnWidth(index, size);
                            }
                            else
                            {
                                SetRowHeight(index, size);
                            }
                        }
                        RebuildGrid();
                    });
                    current.Use();
                    Repaint();
                }
                else if (current.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = 0;
                    _resizeIndex = -1;
                    if (_resizeHasHistory)
                    {
                        Run(StoreDimensions);
                    }
                    _resizeHasHistory = false;
                    current.Use();
                }
                return;
            }

            for (int column = FindIndex(_columnEdges, _scroll.x); column < _columns; column++)
            {
                float x = area.x + RowHeaderWidth + _columnEdges[column + 1] - _scroll.x;
                if (x > area.xMax - AddHeaderSize - 4)
                {
                    break;
                }
                var edge = new Rect(x - 4, area.y, 8, HeaderHeight);
                EditorGUIUtility.AddCursorRect(edge, MouseCursor.ResizeHorizontal);
                if (current.type == EventType.MouseDown && current.button == 0 && edge.Contains(current.mousePosition))
                {
                    StartResize(column, true, control);
                    return;
                }
            }
            for (int row = FindIndex(_rowEdges, _scroll.y); row < _rows; row++)
            {
                float y = area.y + HeaderHeight + _rowEdges[row + 1] - _scroll.y;
                if (y > area.yMax - AddHeaderSize - 4)
                {
                    break;
                }
                var edge = new Rect(area.x, y - 4, RowHeaderWidth, 8);
                EditorGUIUtility.AddCursorRect(edge, MouseCursor.ResizeVertical);
                if (current.type == EventType.MouseDown && current.button == 0 && edge.Contains(current.mousePosition))
                {
                    StartResize(row, false, control);
                    return;
                }
            }
        }


        private void StartResize(int index, bool column, int control)
        {
            if (Event.current.clickCount == 2)
            {
                AutoFitHeader(index, column);
                Event.current.Use();
                return;
            }
            Run(CommitEdit);
            _editingInline = false;
            _resizeHasHistory = false;
            _resizeIndex = index;
            _resizeColumn = column;
            int first = column ? MinColumn : MinRow;
            int last = column ? MaxColumn : MaxRow;
            _resizeFirst = index >= first && index <= last ? first : index;
            _resizeLast = index >= first && index <= last ? last : index;
            _resizePointer = column ? Event.current.mousePosition.x : Event.current.mousePosition.y;
            _resizeOriginalSize = column ? ColumnWidth(index) : RowHeight(index);
            GUIUtility.hotControl = control;
            Event.current.Use();
        }


        private void SetColumnWidth(int column, float pixels)
        {
            pixels = Mathf.Clamp(pixels, MinColumnWidth, MaxColumnWidth);
            if (_document.IsCsv)
            {
                if (column >= _csvLayout.columnWidths.Length)
                {
                    Array.Resize(ref _csvLayout.columnWidths, column + 1);
                }
                _csvLayout.columnWidths[column] = pixels;
            }
            else
            {
                _document.SetColumnWidth(column, pixels);
                _excelColumnWidths[column] = pixels;
            }
        }


        private void SetRowHeight(int row, float pixels)
        {
            pixels = Mathf.Clamp(pixels, MinRowHeight, MaxRowHeight);
            if (_document.IsCsv)
            {
                if (row >= _csvLayout.rowHeights.Length)
                {
                    Array.Resize(ref _csvLayout.rowHeights, row + 1);
                }
                _csvLayout.rowHeights[row] = pixels;
            }
            else
            {
                _document.SetRowHeight(row, pixels);
                _excelRowHeights[row] = pixels;
            }
        }


        private void StoreDimensions()
        {
            CaptureHistory();
            if (_document.IsCsv)
            {
                StoreCsvLayout();
            }
            else
            {
                StoreDraft();
            }
        }

        // 面板高度只影响 Inspector，不属于表格数据；松开拖动条时保存个人布局。
        private void DrawPanelResizeHandle(int control, Rect rect)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            EditorGUI.DrawRect(rect, HeaderBackground);
            EditorGUI.DrawRect(new Rect(rect.center.x - 18, rect.center.y - 1, 36, 2), Accent);
            GUI.Label(rect, new GUIContent("", "Drag up or down to resize the grid. The height is remembered for this file."));
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                _resizingPanel = true;
                _panelPointer = current.mousePosition.y;
                _panelOriginalHeight = _gridHeight;
                GUIUtility.hotControl = control;
                current.Use();
            }
            else if (_resizingPanel && GUIUtility.hotControl == control)
            {
                if (current.type == EventType.MouseDrag)
                {
                    SetPanelHeight(_panelOriginalHeight + current.mousePosition.y - _panelPointer);
                    current.Use();
                    Repaint();
                }
                else if (current.type == EventType.MouseUp)
                {
                    _resizingPanel = false;
                    Run(StoreViewLayout);
                    GUIUtility.hotControl = 0;
                    current.Use();
                }
            }
        }


        // 二分查找从滚动位置定位首个可见行列，不需要从第一行逐个累加。
        private static int FindIndex(float[] edges, float position)
        {
            int index = Array.BinarySearch(edges, position);
            // 没有命中边界时，BinarySearch 返回插入位置的按位取反；减一得到所在单元格的左边界。
            return Mathf.Clamp(index >= 0 ? index : ~index - 1, 0, edges.Length - 2);
        }

    }
}
