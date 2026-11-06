using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace XlsxInspector
{
    public partial class SpreadsheetInspector
    {
        private bool _showDimensions = true; // 默认显示尺寸设置，可关闭并通过 Format 菜单重新展开。
        private Rect _dimensionsRect; // 最近一次重绘时的尺寸设置区域，折叠时为空。
        private Rect _copyButtonRect; // Copy 按钮的实际位置，用于验证长按时滚动区保持稳定。
        private Rect _findButtonRect; // 工具栏查找入口的位置，关闭搜索后仍可通过鼠标重新打开。

        // 界面按工具栏、编辑区和网格分段，便于初次阅读 IMGUI 代码时追踪调用顺序。
        private void DrawSpreadsheet()
        {
            if (_document == null)
            {
                EditorGUILayout.HelpBox(_error ?? "Unable to open spreadsheet.", MessageType.Error);
                if (GUILayout.Button(new GUIContent("Retry", "Try opening this file again after resolving the error.")))
                {
                    Run(Load);
                }
                return;
            }

            // 在可见行列的按钮之前分配拖动条 ID，行数变化时仍由同一个控件持有鼠标。
            PrepareAppearance();
            int panelResizeControl = GUIUtility.GetControlID(FocusType.Passive);
            // 先为工具栏和输入区占位，再登记网格滚动条。输入消费事件时，不会影响后面的布局或控件 ID。
            Rect toolbar = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 4, GUILayout.ExpandWidth(true));
            if (!_writable)
            {
                EditorGUILayout.HelpBox("This file is read-only. Copy it into Assets to edit it.", MessageType.Info);
            }
            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
            Rect formula = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
            Rect actions = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 4, GUILayout.ExpandWidth(true));
            Rect insertion = _showInsertion
                ? GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 4 + 14, GUILayout.ExpandWidth(true)) : Rect.zero;
            Rect dimensions = _showDimensions
                ? GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 6, GUILayout.ExpandWidth(true)) : Rect.zero;
            // 查找框先占位，控件最后绘制。折叠替换行或按下条件按钮都不会改变滚动控件的 ID。
            Rect search = _showSearch ? GUILayoutUtility.GetRect(0, SearchHeight(), GUILayout.ExpandWidth(true)) : Rect.zero;
            if (_editingInline && Event.current.type == EventType.MouseDown && search.Contains(Event.current.mousePosition))
            {
                Run(CommitEdit);
                _editingInline = false;
            }
            // 先确定所有区域，再创建滚动控件，最后处理按钮和文本输入。
            Rect grid = GUILayoutUtility.GetRect(0, _gridHeight, GUILayout.ExpandWidth(true));
            Rect resize = GUILayoutUtility.GetRect(0, 8, GUILayout.ExpandWidth(true));
            Rect sheets = _document.IsCsv ? Rect.zero
                : GUILayoutUtility.GetRect(0, SheetTabHeight + 18, GUILayout.ExpandWidth(true));
            RefreshSearch();
            DrawGrid(grid);
            DrawSheetTabs(sheets);
            HandleKeyboard();
            DrawToolbar(toolbar);
            DrawFormulaBar(formula);
            DrawActions(actions);
            DrawInsertionControls(insertion);
            // 尺寸输入也在滚动控件之后登记，展开和收起时不会改变页签或网格滚动条的 ID。
            DrawDimensions(dimensions);
            // 搜索在滚动控件之后、可变数量的表头按钮之前登记，滚动状态和输入框焦点都保持稳定。
            DrawSearch(search);
            DrawGridCells(grid);
            DrawHeaders(grid);
            DrawPanelResizeHandle(panelResizeControl, resize);
            FinishEditingOnFocusChange();
            if (SpreadsheetSettings.instance.ShowHints)
            {
                GUILayout.Label("Double-click to edit • Shift-click to select • Drag header edges to resize", EditorStyles.miniLabel);
            }
            GUILayout.Label(new GUIContent($"Data: {_dataRows} rows × {_dataColumns} columns  ·  Selection: {MaxRow - MinRow + 1} × {MaxColumn - MinColumn + 1}",
                "The bright area follows actual content, including formulas and spaces. Clearing edge cells shrinks it automatically.\nDimmed cells accept typing and pasting; blank rows and columns inside the content range are kept."), EditorStyles.miniLabel);
            if (Event.current.type == EventType.Repaint)
            {
                // 记录真实的尾部占用，调整高度时始终为拖动条、Sheet 标签和状态文字留出位置。
                _gridFooterHeight = GUILayoutUtility.GetLastRect().yMax - _gridRect.yMax;
                UpdatePanelHeightLimit();
            }
        }


        // 工具栏仅使用已分配的矩形，按钮消费鼠标事件也不会改变其余区域的位置。
        private void DrawToolbar(Rect area)
        {
            var button = new Rect(area.x + 2, area.y + 2, 48, EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(!_writable))
            {
                if (GUI.Button(button, new GUIContent("Save", "Write all changes to the original file (Ctrl/Cmd+S). Excel dimensions and formatting are included; CSV display settings are saved separately."), EditorStyles.miniButton))
                {
                    Save();
                }
                button.x = button.xMax + 3;
                if (GUI.Button(button, new GUIContent("Undo", "Undo the last edit, format change or row/column operation (Ctrl/Cmd+Z)."), EditorStyles.miniButton))
                {
                    PerformHistory(false);
                }
                button.x = button.xMax + 3;
                if (GUI.Button(button, new GUIContent("Redo", "Reapply the last undone operation (Ctrl+Y or Ctrl/Cmd+Shift+Z)."), EditorStyles.miniButton))
                {
                    PerformHistory(true);
                }
            }
            button.x = button.xMax + 3;
            button.width = 52;
            if (GUI.Button(button, new GUIContent("Reload", "Read the file from disk again. You will be asked before pending edits are discarded."), EditorStyles.miniButton))
            {
                Reload();
            }
            button.x = button.xMax + 3;
            button.width = 58;
            if (GUI.Button(button, new GUIContent("Settings", "Open Project Settings for layout defaults, appearance, tooltips and selection scrolling."), EditorStyles.miniButton))
            {
                SettingsService.OpenProjectSettings("Project/Unity Xlsx Inspector");
            }
            button.x = button.xMax + 3;
            button.width = 40;
            if (GUI.Button(button, new GUIContent("Find", "Open and focus Find (Ctrl/Cmd+F). Expand the arrow in the search bar for Replace, or press Ctrl/Cmd+H."), EditorStyles.miniButton))
            {
                OpenSearch();
            }
            if (Event.current.type == EventType.Repaint)
            {
                _findButtonRect = button;
            }
            bool dirty = _document.IsDirty || _editChanged;
            var status = new GUIContent(dirty ? "● Unsaved" : "Saved", dirty ? "Changes are kept in a local draft. Click Save to update the original file." : "The original file is up to date.");
            float statusWidth = EditorStyles.miniLabel.CalcSize(status).x;
            GUI.Label(new Rect(area.xMax - statusWidth - 2, button.y, statusWidth, button.height), status, EditorStyles.miniLabel);
        }

        // 编辑栏保留多行输入，与单元格内编辑器共用同一个缓冲区和提交入口。
        private void DrawFormulaBar(Rect area)
        {
            using (new EditorGUI.DisabledScope(!_writable))
            {
                GUI.Label(new Rect(area.x + 2, area.y + 2, 48, 38), new GUIContent(Address(_selected.y, _selected.x), "Address of the active cell. Letters identify columns; numbers identify rows."), EditorStyles.helpBox);
                var input = new Rect(area.x + 54, area.y + 2, area.width - 104, 38);
                GUI.SetNextControlName(FormulaControl);
                EditorGUI.BeginChangeCheck();
                _editBuffer = GUI.TextArea(input, _editBuffer);
                if (EditorGUI.EndChangeCheck())
                {
                    _editChanged = true;
                }
                GUI.Label(input, new GUIContent("", "Edit the active cell, including multiline text. In XLSX, start with = for a formula or ' to force text. Apply commits the edit; Save writes the file."));
                if (GUI.Button(new Rect(area.xMax - 46, area.y + 2, 44, 38), new GUIContent("Apply", "Commit the text to the active cell. Use Save to write changes to the original file.")))
                {
                    Run(CommitEdit);
                }
            }
        }

        // 为较长的菜单标题保留宽度，再把剩余空间平均分给六个按钮。
        private void DrawActions(Rect area)
        {
            float extraWidth = (area.width - 369) / 6;
            var button = new Rect(area.x + 2, area.y + 2, 40 + extraWidth, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(button, new GUIContent("Copy", "Copy the selected cells as tab-separated text (Ctrl/Cmd+C). Paste directly into Excel or another spreadsheet."), EditorStyles.miniButton))
            {
                Copy();
            }
            if (Event.current.type == EventType.Repaint)
            {
                _copyButtonRect = button;
            }
            using (new EditorGUI.DisabledScope(!_writable))
            {
                button.x = button.xMax + 3;
                if (GUI.Button(button, new GUIContent("Paste", "Paste starting at the active cell (Ctrl/Cmd+V). The table extends to fit, including when pasting into dimmed cells."), EditorStyles.miniButton))
                {
                    Change(() => _document.Paste(_selected.y, _selected.x, EditorGUIUtility.systemCopyBuffer));
                }
                button.x = button.xMax + 3;
                if (GUI.Button(button, new GUIContent("Clear", "Clear the selected cell contents (Delete/Backspace), keeping dimensions and formatting. The highlighted content range shrinks automatically."), EditorStyles.miniButton))
                {
                    Change(Clear);
                }
                button.x = button.xMax + 3;
                button.width = 58 + extraWidth;
                if (GUI.Button(button, new GUIContent("Auto Fit", "Fit selected columns and rows to their contents using the grid font size. You can also double-click a header edge."), EditorStyles.miniButton))
                {
                    AutoFit(false);
                }
                button.x = button.xMax + 3;
                button.width = 60 + extraWidth;
                if (GUI.Button(button, new GUIContent("Format ▾", "Adjust column width, row height, alignment or wrapping for the selection. Also available by right-clicking a cell."), EditorStyles.miniButton))
                {
                    var menu = new GenericMenu();
                    AddFormatMenuItems(menu);
                    menu.DropDown(button);
                }
                button.x = button.xMax + 3;
                button.width = 112 + extraWidth;
                if (GUI.Button(button, new GUIContent("Rows / Columns ▾", "Insert or delete rows/columns to shift existing data. Use Clear to keep cells in place. The header + buttons insert below or to the right of the selection."), EditorStyles.miniButton))
                {
                    ShowStructureMenu();
                }
            }
        }

        // 尺寸区域默认展开，也允许临时收起；原有 ResizeSelection 仍负责批量修改和 Undo。
        private void DrawDimensions(Rect area)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _dimensionsRect = area;
            }
            if (!_showDimensions)
            {
                return;
            }
            float fieldWidth = (area.width - 144) / 2;
            float top = area.y + 3;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            var widthLabel = new Rect(area.x + 4, top, 38, lineHeight);
            var widthField = new Rect(widthLabel.xMax, top, fieldWidth, lineHeight);
            var heightLabel = new Rect(widthField.xMax + 8, top, 42, lineHeight);
            var heightField = new Rect(heightLabel.xMax, top, fieldWidth, lineHeight);
            using (new EditorGUI.DisabledScope(!_writable))
            {
                float width = ColumnWidth(_selected.x);
                float height = RowHeight(_selected.y);
                GUI.Label(widthLabel, new GUIContent("Width", "Width in pixels. Enter a value to resize every selected column; press Enter or leave the field to apply."));
                EditorGUI.BeginChangeCheck();
                float newWidth = EditorGUI.DelayedFloatField(widthField, (float)Math.Round(width, 2));
                bool widthChanged = EditorGUI.EndChangeCheck();
                GUI.Label(heightLabel, new GUIContent("Height", "Height in pixels. Enter a value to resize every selected row; press Enter or leave the field to apply."));
                EditorGUI.BeginChangeCheck();
                float newHeight = EditorGUI.DelayedFloatField(heightField, (float)Math.Round(height, 2));
                bool heightChanged = EditorGUI.EndChangeCheck();
                GUI.Label(new Rect(heightField.xMax + 4, top, 18, lineHeight), "px");
                if (widthChanged || heightChanged)
                {
                    ResizeSelection(widthChanged ? newWidth : (float?)null, heightChanged ? newHeight : (float?)null);
                }
            }
            if (GUI.Button(new Rect(area.xMax - 21, top, 18, lineHeight), new GUIContent("×", "Hide column width and row height. Open them again from Format."), EditorStyles.miniButton))
            {
                _showDimensions = false;
            }
        }


    }
}
