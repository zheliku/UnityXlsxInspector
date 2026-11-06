using System;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    public partial class SpreadsheetInspector
    {

        // 编辑提交与选择。读取、复制不会创建修改；只有提交不同文本后才生成草稿。
        private void Select(int row, int column, bool extend)
        {
            CommitEdit();
            _editingInline = false;
            _selected = new Vector2Int(column, row);
            if (!extend)
            {
                _anchor = _selected;
            }
            _editBuffer = _document.GetText(row, column, true);
        }


        private void SelectRange(Vector2Int first, Vector2Int last)
        {
            Run(() =>
            {
                CommitEdit();
                _editingInline = false;
                _anchor = first;
                _selected = last;
                _editBuffer = _document.GetText(last.y, last.x, true);
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
            });
        }

        // 全选取实际内容的矩形边界，不把滚动预览和已经清空的尾部空格一起复制出去。
        private void SelectAll()
        {
            Run(() =>
            {
                CommitEdit();
                SelectRange(Vector2Int.zero, new Vector2Int(Math.Max(0, _dataColumns - 1), Math.Max(0, _dataRows - 1)));
            });
        }


        private void BeginInlineEdit(string replacement = null)
        {
            _editingInline = _focusInline = true;
            if (replacement != null)
            {
                _editBuffer = replacement;
                _editChanged = true;
            }
            RevealSelection();
            Repaint();
        }


        private void CommitEdit()
        {
            if (!_editChanged)
            {
                return;
            }
            if (_editBuffer == _document.GetText(_selected.y, _selected.x, true))
            {
                _editChanged = false;
                return;
            }
            BeginHistory("Edit spreadsheet cell");
            _document.SetText(_selected.y, _selected.x, _editBuffer);
            _editChanged = false;
            // 输入可能发生在最后一行列的预留空格；提交后立即补出下一格，供 Tab 和方向键继续移动。
            RebuildGrid();
            CaptureHistory();
            StoreDraft();
        }


        private void FinishEditingOnFocusChange()
        {
            string focus = GUI.GetNameOfFocusedControl();
            if (_focusInline || focus == InlineControl || focus == FormulaControl)
            {
                return;
            }
            if (_editingInline || _editChanged)
            {
                Run(() =>
                {
                    CommitEdit();
                    _editingInline = false;
                });
            }
        }


        /// <summary>把当前选区复制到系统剪贴板；此操作不修改工作簿和原文件。</summary>
        internal void Copy()
        {
            Run(() =>
            {
                CommitEdit();
                EditorGUIUtility.systemCopyBuffer = _document.Copy(MinRow, MinColumn, MaxRow, MaxColumn);
            });
        }


        private void Clear()
        {
            for (int row = MinRow; row <= MaxRow; row++)
            {
                for (int column = MinColumn; column <= MaxColumn; column++)
                {
                    _document.SetText(row, column, "");
                }
            }
        }


        private void Change(Action action, string name = "Edit spreadsheet", bool affectsDocument = true)
        {
            Run(() =>
            {
                CommitEdit();
                _editingInline = false;
                BeginHistory(name, affectsDocument);
                action();
                RebuildGrid();
                _editBuffer = _document.GetText(_selected.y, _selected.x, true);
                CaptureHistory();
                StoreDraft();
                StoreCsvLayout();
                Repaint();
            });
        }




        private void ShowCellMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy"), false, Copy);
            if (_writable)
            {
                menu.AddItem(new GUIContent("Cut"), false, () => { Copy(); Change(Clear); });
                menu.AddItem(new GUIContent("Paste"), false,
                    () => Change(() => _document.Paste(_selected.y, _selected.x, EditorGUIUtility.systemCopyBuffer)));
                menu.AddItem(new GUIContent("Clear"), false, () => Change(Clear));
                menu.AddSeparator("");
                AddFormatMenuItems(menu);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Auto Fit/Selection width and height"), false, () => AutoFit(false));
                menu.AddItem(new GUIContent("Auto Fit/Entire sheet"), false, () => AutoFit(true));
            }
            menu.ShowAsContext();
        }


        private void FindNext()
        {
            Run(() =>
            {
                CommitEdit();
                int width = _document.ColumnCount;
                long count = (long)_document.RowCount * width;
                long start = (long)_selected.y * width + _selected.x;
                for (long step = 1; step <= count; step++)
                {
                    long index = (start + step) % count;
                    int row = (int)(index / width);
                    int column = (int)(index % width);
                    if (_document.GetText(row, column, true).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Select(row, column, false);
                        RevealSelection();
                        Repaint();
                        return;
                    }
                }
                _error = "No matching cells on this sheet.";
            });
        }


        // 格内编辑优先处理 Enter / Tab / Esc，避免 TextArea 把提交键当成普通换行。
        // 编辑栏保留多行文本编辑；不在文本框内时才处理选区复制和方向键移动。
        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
            {
                return;
            }
            bool command = current.control || current.command;
            if (command && (current.keyCode == KeyCode.Z || current.keyCode == KeyCode.Y))
            {
                bool redo = current.keyCode == KeyCode.Y || current.shift;
                PerformHistory(redo);
                current.Use();
                Repaint();
                return;
            }
            if (command && current.keyCode == KeyCode.S)
            {
                Save();
                current.Use();
                return;
            }
            if (_editingInline)
            {
                if (current.keyCode == KeyCode.Escape)
                {
                    _editBuffer = _document.GetText(_selected.y, _selected.x, true);
                    _editChanged = _editingInline = false;
                    GUI.FocusControl(null);
                    EditorGUIUtility.editingTextField = false;
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Tab || (current.keyCode == KeyCode.Return && !current.alt))
                {
                    bool tab = current.keyCode == KeyCode.Tab;
                    int direction = current.shift ? -1 : 1;
                    MoveSelection(tab ? direction : 0, tab ? 0 : direction, false);
                    current.Use();
                }
                return;
            }
            if (EditorGUIUtility.editingTextField)
            {
                return;
            }

            bool handled = true;
            if (command && current.keyCode == KeyCode.C)
            {
                Copy();
            }
            else if (command && current.keyCode == KeyCode.V && _writable)
            {
                Change(() => _document.Paste(_selected.y, _selected.x, EditorGUIUtility.systemCopyBuffer));
            }
            else if (command && current.keyCode == KeyCode.X && _writable)
            {
                Copy();
                Change(Clear);
            }
            else if (command && current.keyCode == KeyCode.A)
            {
                SelectAll();
            }
            else if ((current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace) && _writable)
            {
                Change(Clear);
            }
            else if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.F2) && _writable)
            {
                BeginInlineEdit();
            }
            else if (current.keyCode == KeyCode.UpArrow)
            {
                MoveSelection(0, -1, current.shift);
            }
            else if (current.keyCode == KeyCode.DownArrow)
            {
                MoveSelection(0, 1, current.shift);
            }
            else if (current.keyCode == KeyCode.LeftArrow)
            {
                MoveSelection(-1, 0, current.shift);
            }
            else if (current.keyCode == KeyCode.RightArrow || current.keyCode == KeyCode.Tab)
            {
                MoveSelection(current.keyCode == KeyCode.Tab && current.shift ? -1 : 1, 0,
                    current.keyCode != KeyCode.Tab && current.shift);
            }
            else if (_writable && !command && !current.alt && current.character >= ' ')
            {
                BeginInlineEdit(current.character.ToString());
            }
            else
            {
                handled = false;
            }
            if (handled)
            {
                current.Use();
                Repaint();
            }
        }


        private void MoveSelection(int columns, int rows, bool extend)
        {
            Run(() =>
            {
                // 提交后网格才包含新预留行列，必须先提交，再用最新边界限制移动目标。
                CommitEdit();
                int row = Mathf.Clamp(_selected.y + rows, 0, _document.MaxRows - 1);
                int column = Mathf.Clamp(_selected.x + columns, 0, _document.MaxColumns - 1);
                EnsurePreviewCell(row, column);
                Select(row, column, extend);
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
                RevealSelection();
            });
        }


        private void RevealSelection()
        {
            // 选区可能位于已回收的灰色尾部；重新定位它时先恢复所需坐标，再计算滚动距离。
            EnsurePreviewCell(_selected.y, _selected.x);
            float left = _columnEdges[_selected.x];
            float right = _columnEdges[_selected.x + 1];
            float top = _rowEdges[_selected.y];
            float bottom = _rowEdges[_selected.y + 1];
            if (left < _scroll.x)
            {
                _scroll.x = left;
            }
            else if (right > _scroll.x + _viewportSize.x)
            {
                _scroll.x = Math.Max(0, right - _viewportSize.x);
            }
            if (top < _scroll.y)
            {
                _scroll.y = top;
            }
            else if (bottom > _scroll.y + _viewportSize.y)
            {
                _scroll.y = Math.Max(0, bottom - _viewportSize.y);
            }
        }


        private static string Address(int row, int column)
        {
            string name = "";
            for (int value = column + 1; value > 0; value = (value - 1) / 26)
            {
                name = (char)('A' + (value - 1) % 26) + name;
            }
            return name + (row + 1);
        }

    }
}
