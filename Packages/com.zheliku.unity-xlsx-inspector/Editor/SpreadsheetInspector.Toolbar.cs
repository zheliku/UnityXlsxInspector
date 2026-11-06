using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace XlsxInspector
{
    public partial class SpreadsheetInspector
    {

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
            DrawToolbar();
            if (!_writable)
            {
                EditorGUILayout.HelpBox("This file is read-only. Copy it into Assets to edit it.", MessageType.Info);
            }
            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }

            DrawFormulaBar();
            DrawActions();
            DrawInsertionControls();
            DrawDimensions();
            DrawSearch();
            HandleKeyboard();
            // 先固定本帧的几何区域，再画 Sheet 控件。其 ID 不再受可见行号按钮数量影响。
            Rect grid = GUILayoutUtility.GetRect(0, _gridHeight, GUILayout.ExpandWidth(true));
            Rect resize = GUILayoutUtility.GetRect(0, 8, GUILayout.ExpandWidth(true));
            Rect sheets = _document.IsCsv ? Rect.zero
                : GUILayoutUtility.GetRect(0, SheetTabHeight + 18, GUILayout.ExpandWidth(true));
            DrawSheetTabs(sheets);
            DrawGrid(grid);
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


        private void DrawToolbar()
        {
            Rect accentLine = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(accentLine, Accent);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!_writable))
                {
                    if (GUILayout.Button(new GUIContent("Save", "Write all changes to the original file (Ctrl/Cmd+S). Excel dimensions and formatting are included; CSV display settings are saved separately."), EditorStyles.toolbarButton, GUILayout.Width(48)))
                    {
                        Save();
                    }
                    if (GUILayout.Button(new GUIContent("Undo", "Undo the last edit, format change or row/column operation (Ctrl/Cmd+Z)."), EditorStyles.toolbarButton, GUILayout.Width(48)))
                    {
                        PerformHistory(false);
                    }
                    if (GUILayout.Button(new GUIContent("Redo", "Reapply the last undone operation (Ctrl+Y or Ctrl/Cmd+Shift+Z)."), EditorStyles.toolbarButton, GUILayout.Width(48)))
                    {
                        PerformHistory(true);
                    }
                }
                if (GUILayout.Button(new GUIContent("Reload", "Read the file from disk again. You will be asked before pending edits are discarded."), EditorStyles.toolbarButton, GUILayout.Width(52)))
                {
                    Reload();
                }
                if (GUILayout.Button(new GUIContent("Settings", "Open Project Settings for layout defaults, appearance, tooltips and selection scrolling."), EditorStyles.toolbarButton, GUILayout.Width(58)))
                {
                    SettingsService.OpenProjectSettings("Project/Unity Xlsx Inspector");
                }
                GUILayout.FlexibleSpace();
                bool dirty = _document.IsDirty || _editChanged;
                GUILayout.Label(new GUIContent(dirty ? "● Unsaved" : "Saved", dirty ? "Changes are kept in a local draft. Click Save to update the original file." : "The original file is up to date."), EditorStyles.miniLabel);
            }
        }


        private void DrawFormulaBar()
        {
            using (new EditorGUI.DisabledScope(!_writable))
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(Address(_selected.y, _selected.x), "Address of the active cell. Letters identify columns; numbers identify rows."), EditorStyles.helpBox, GUILayout.Width(48), GUILayout.Height(38));
                GUI.SetNextControlName(FormulaControl);
                EditorGUI.BeginChangeCheck();
                _editBuffer = EditorGUILayout.TextArea(_editBuffer, GUILayout.MinHeight(38));
                if (EditorGUI.EndChangeCheck())
                {
                    _editChanged = true;
                }
                GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Edit the active cell, including multiline text. In XLSX, start with = for a formula or ' to force text. Apply commits the edit; Save writes the file."));
                if (GUILayout.Button(new GUIContent("Apply", "Commit the text to the active cell. Use Save to write changes to the original file."), GUILayout.Width(44), GUILayout.Height(38)))
                {
                    Run(CommitEdit);
                }
            }
        }


        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("Copy", "Copy the selected cells as tab-separated text (Ctrl/Cmd+C). Paste directly into Excel or another spreadsheet."), EditorStyles.toolbarButton))
                {
                    Copy();
                }
                using (new EditorGUI.DisabledScope(!_writable))
                {
                    if (GUILayout.Button(new GUIContent("Paste", "Paste starting at the active cell (Ctrl/Cmd+V). The table extends to fit, including when pasting into dimmed cells."), EditorStyles.toolbarButton))
                    {
                        Change(() => _document.Paste(_selected.y, _selected.x, EditorGUIUtility.systemCopyBuffer));
                    }
                    if (GUILayout.Button(new GUIContent("Clear", "Clear the selected cell contents (Delete/Backspace), keeping dimensions and formatting. The highlighted content range shrinks automatically."), EditorStyles.toolbarButton))
                    {
                        Change(Clear);
                    }
                    if (GUILayout.Button(new GUIContent("Auto Fit", "Fit selected columns and rows to their contents using the grid font size. You can also double-click a header edge."), EditorStyles.toolbarButton))
                    {
                        AutoFit(false);
                    }
                    if (GUILayout.Button(new GUIContent("Format ▾", "Set horizontal alignment, vertical alignment or wrapping for all selected cells. These actions are also available by right-clicking a cell."), EditorStyles.toolbarDropDown))
                    {
                        var menu = new GenericMenu();
                        AddFormatMenuItems(menu);
                        menu.ShowAsContext();
                    }
                    if (GUILayout.Button(new GUIContent("Rows / Columns ▾", "Insert or delete rows/columns to shift existing data. Use Clear to keep cells in place. The header + buttons insert below or to the right of the selection."), EditorStyles.toolbarDropDown))
                    {
                        ShowStructureMenu();
                    }
                }
            }
        }


        private void DrawDimensions()
        {
            using (new EditorGUI.DisabledScope(!_writable))
            using (new EditorGUILayout.HorizontalScope())
            {
                float width = ColumnWidth(_selected.x);
                float height = RowHeight(_selected.y);
                GUILayout.Label(new GUIContent("Width", "Width in pixels. Enter a value to resize every selected column; press Enter or leave the field to apply."), GUILayout.Width(40));
                EditorGUI.BeginChangeCheck();
                float newWidth = EditorGUILayout.DelayedFloatField((float)Math.Round(width, 2), GUILayout.MinWidth(45));
                bool widthChanged = EditorGUI.EndChangeCheck();
                GUILayout.Label(new GUIContent("Height", "Height in pixels. Enter a value to resize every selected row; press Enter or leave the field to apply."), GUILayout.Width(43));
                EditorGUI.BeginChangeCheck();
                float newHeight = EditorGUILayout.DelayedFloatField((float)Math.Round(height, 2), GUILayout.MinWidth(45));
                bool heightChanged = EditorGUI.EndChangeCheck();
                GUILayout.Label("px", GUILayout.Width(18));
                if (widthChanged || heightChanged)
                {
                    ResizeSelection(widthChanged ? newWidth : (float?)null, heightChanged ? newHeight : (float?)null);
                }
            }
        }


        private void DrawSearch()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Search cell text in the current worksheet, ignoring letter case. Matching cells have an amber marker."));
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                {
                    if (GUILayout.Button(new GUIContent("Find Next", "Select the next matching cell in the current worksheet. Continues from the beginning after the last match."), GUILayout.Width(76)))
                    {
                        FindNext();
                    }
                }
            }
        }

    }
}
