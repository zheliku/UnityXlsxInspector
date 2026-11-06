using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    // 单行列、批量插入和表头加号共用同一修改入口，一次插入只记录一个 Undo 步骤。
    public partial class SpreadsheetInspector
    {
        private bool _showInsertion; // 是否在 Inspector 操作栏下面展开批量插入设置。
        private bool _insertRows; // true 插入行，false 插入列。
        private int _insertCount = 1; // 批量插入的数量，由用户输入。
        private int _insertPosition; // 0 在选区之前，1 在选区之后，2 在整个工作区末尾。

        private void ShowStructureMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Insert row above"), false, () => InsertStructure(MinRow, true, 1));
            menu.AddItem(new GUIContent("Insert row below"), false, () => InsertStructure(MaxRow + 1, true, 1));
            menu.AddItem(new GUIContent("Insert multiple rows..."), false, () => ShowInsertion(true, 0));
            menu.AddItem(new GUIContent("Delete selected rows"), false, () => Change(() =>
            {
                for (int row = MaxRow; row >= MinRow; row--)
                {
                    EditStructure(row, true, false);
                }
            }));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Insert column left"), false, () => InsertStructure(MinColumn, false, 1));
            menu.AddItem(new GUIContent("Insert column right"), false, () => InsertStructure(MaxColumn + 1, false, 1));
            menu.AddItem(new GUIContent("Insert multiple columns..."), false, () => ShowInsertion(false, 0));
            menu.AddItem(new GUIContent("Delete selected columns"), false, () => Change(() =>
            {
                for (int column = MaxColumn; column >= MinColumn; column--)
                {
                    EditStructure(column, false, false);
                }
            }));
            menu.ShowAsContext();
        }

        private void ShowInsertion(bool rows, int position)
        {
            _insertRows = rows;
            _insertPosition = position;
            _insertCount = position == 2 ? 1 : rows ? MaxRow - MinRow + 1 : MaxColumn - MinColumn + 1;
            _showInsertion = true;
            Repaint();
        }

        // 使用 Inspector 内联输入区，不为数量输入再创建独立 Editor Window。
        private void DrawInsertionControls(Rect area)
        {
            if (!_showInsertion)
            {
                return;
            }
            using (new EditorGUI.DisabledScope(!_writable))
            {
                GUI.Box(area, GUIContent.none, EditorStyles.helpBox);
                var line = new Rect(area.x + 6, area.y + 4, area.width - 12, EditorGUIUtility.singleLineHeight);
                GUI.Label(line, _insertRows ? "Insert multiple rows" : "Insert multiple columns", EditorStyles.boldLabel);
                line.y += line.height + 2;
                _insertCount = EditorGUI.IntField(line, new GUIContent("Count", "Number of rows or columns to insert. The whole operation can be undone in one step."), _insertCount);
                line.y += line.height + 2;
                _insertPosition = EditorGUI.Popup(line, new GUIContent("Position", "Insert beside the selection, or after the allocated table range, including previously appended blank rows and columns."), _insertPosition, _insertRows
                    ? new[] { new GUIContent("Above selection"), new GUIContent("Below selection"), new GUIContent("End of table") }
                    : new[] { new GUIContent("Left of selection"), new GUIContent("Right of selection"), new GUIContent("End of table") });
                line.y += line.height + 2;
                float buttonWidth = (line.width - 4) / 2;
                using (new EditorGUI.DisabledScope(_insertCount <= 0))
                {
                    if (GUI.Button(new Rect(line.x, line.y, buttonWidth, line.height), new GUIContent("Insert", "Insert the requested rows or columns. Ctrl/Cmd+Z undoes the batch.")))
                    {
                        int index = _insertPosition == 2 ? (_insertRows ? _document.RowCount : _document.ColumnCount)
                            : _insertRows ? (_insertPosition == 0 ? MinRow : MaxRow + 1)
                            : (_insertPosition == 0 ? MinColumn : MaxColumn + 1);
                        InsertStructure(index, _insertRows, _insertCount);
                        _showInsertion = !string.IsNullOrEmpty(_error);
                    }
                }
                if (GUI.Button(new Rect(line.xMax - buttonWidth, line.y, buttonWidth, line.height), new GUIContent("Cancel", "Close the insertion controls without inserting.")))
                {
                    _showInsertion = false;
                }
            }
        }

        private void InsertStructure(int index, bool rows, int count)
        {
            Run(() =>
            {
                CommitEdit();
                // 在记录 Undo 和写入之前验证数量；边界错误不会留下半次插入。
                _document.ValidateInsertion(index, count, rows);
                Change(() =>
                {
                    EditStructure(index, rows, true, count);
                    _anchor = rows ? new Vector2Int(0, index) : new Vector2Int(index, 0);
                    _selected = rows ? new Vector2Int(_document.ColumnCount - 1, index + count - 1)
                        : new Vector2Int(index + count - 1, _document.RowCount - 1);
                }, rows ? "Insert spreadsheet rows" : "Insert spreadsheet columns");
                if (string.IsNullOrEmpty(_error))
                {
                    GUIUtility.keyboardControl = 0;
                    EditorGUIUtility.editingTextField = false;
                    Vector2 previousScroll = _scroll;
                    RevealSelection();
                    // 新增行只纵向定位，新增列只横向定位，不跳到另一条轴的表格末端。
                    _scroll = rows ? new Vector2(previousScroll.x, _scroll.y) : new Vector2(_scroll.x, previousScroll.y);
                }
            });
        }

        private void InsertAfterSelection(bool rows)
        {
            Run(() =>
            {
                // 加号用于插入并移动后续数据；空白网格本身会随滚动延伸，不再要求先追加空行列。
                CommitEdit();
                InsertStructure(rows ? MaxRow + 1 : MaxColumn + 1, rows, 1);
            });
        }

        private void DrawInsertButton(Rect rect, bool rows)
        {
            int size = rows ? _document.RowCount : _document.ColumnCount;
            int index = rows ? MaxRow + 1 : MaxColumn + 1;
            bool canInsert = _writable && Math.Max(size, index) < (rows ? _document.MaxRows : _document.MaxColumns);
            Event current = Event.current;
            if (canInsert && current.type == EventType.ContextClick && rect.Contains(current.mousePosition))
            {
                ShowInsertion(rows, 1);
                current.Use();
            }
            using (new EditorGUI.DisabledScope(!canInsert))
            {
                string tooltip = rows ? "Insert a row below the selection, shifting later rows down. Right-click to insert multiple rows. You can type in empty cells without inserting."
                    : "Insert a column to the right of the selection, shifting later columns right. Right-click to insert multiple columns. You can type in empty cells without inserting.";
                if (DrawHeaderButton(rect, "+", true, false, tooltip))
                {
                    InsertAfterSelection(rows);
                }
            }
        }

        // Excel 库会随行列移动格式；CSV 的个人格式和尺寸需要在同一个修改步骤中同步移动。
        private void EditStructure(int index, bool rows, bool insert, int count = 1)
        {
            if (rows)
            {
                if (insert)
                {
                    _document.InsertRows(index, count);
                }
                else
                {
                    _document.DeleteRow(index);
                }
            }
            else if (insert)
            {
                _document.InsertColumns(index, count);
            }
            else
            {
                _document.DeleteColumn(index);
            }

            if (!_document.IsCsv)
            {
                return;
            }
            var sizes = new List<float>(rows ? _csvLayout.rowHeights : _csvLayout.columnWidths);
            if (insert)
            {
                if (index > sizes.Count)
                {
                    sizes.AddRange(Enumerable.Repeat(0f, index - sizes.Count));
                }
                sizes.InsertRange(index, Enumerable.Repeat(0f, count));
            }
            else
            {
                if (index < sizes.Count)
                {
                    sizes.RemoveAt(index);
                }
                _csvLayout.cellFormats.RemoveAll(item => (rows ? item.row : item.column) == index);
            }
            if (rows)
            {
                _csvLayout.rowHeights = sizes.ToArray();
            }
            else
            {
                _csvLayout.columnWidths = sizes.ToArray();
            }
            foreach (var item in _csvLayout.cellFormats)
            {
                int current = rows ? item.row : item.column;
                if (current > index || (insert && current == index))
                {
                    if (rows)
                    {
                        item.row += insert ? count : -1;
                    }
                    else
                    {
                        item.column += insert ? count : -1;
                    }
                }
            }
        }
    }
}
