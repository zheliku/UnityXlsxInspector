using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    public partial class SpreadsheetInspector
    {
        private static float SearchRowHeight => EditorGUIUtility.singleLineHeight; // 与 Unity 原生数值输入框保持相同高度。
        private Rect _searchAreaRect; // 最近一次重绘时的搜索面板区域。
        private Rect _findInputRect; // 查找输入框的实际位置，用于检查上下输入区对齐。
        private Rect _replaceInputRect; // 替换输入框的实际位置；折叠时为空。
        private Rect _searchFoldoutRect; // 展开替换行的点击区域。
        private readonly Rect[] _searchOptionRects = new Rect[4]; // 大小写、全词、正则与选区按钮的位置。
        private int _findKeyboardControl; // 当前查找框的键盘控件 ID，供网格绘制前处理 Enter。
        private int _replaceKeyboardControl; // 当前替换框的键盘控件 ID。
        private GUIStyle _searchOptionStyle; // 保留原生圆角边框，选中状态仅用淡色背景和加粗文字区分。
        private GUIStyle _searchActionStyle; // 导航与替换按钮共用原生小按钮样式。
        private GUIStyle _searchHintStyle; // 输入框占位文字靠左显示，字号与输入文字一致。

        private bool HasSearchTextFocus => _showSearch && GUIUtility.keyboardControl != 0
            && (GUIUtility.keyboardControl == _findKeyboardControl || GUIUtility.keyboardControl == _replaceKeyboardControl);

        // 只计算布局高度；输入框在滚动条之后、单元格之前登记，避免按压事件和可见格子数量改变控件 ID。
        private float SearchHeight()
        {
            if (!_showSearch)
            {
                return 0;
            }
            float height = 8 + (_showReplace ? SearchRowHeight * 2 + 3 : SearchRowHeight);
            if (_searchError != null)
            {
                height += Mathf.Max(40, EditorStyles.helpBox.CalcHeight(new GUIContent(_searchError), Mathf.Max(200, _gridRect.width - 12)));
            }
            return height;
        }

        private void DrawSearch(Rect area)
        {
            if (!_showSearch)
            {
                return;
            }
            Event current = Event.current;
            EditorGUI.BeginChangeCheck();
            _searchOptionStyle ??= new GUIStyle(EditorStyles.miniButton) { fixedHeight = 0 };
            _searchActionStyle ??= new GUIStyle(EditorStyles.miniButton) { fixedHeight = 0 };
            _searchHintStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = EditorStyles.textField.fontSize
            };
            GUI.Box(area, GUIContent.none, EditorStyles.helpBox);
            // 保留原来的两行布局：查找条件紧随查找框，替换按钮放在第二行右侧。
            float top = area.y + 4;
            var close = new Rect(area.xMax - 28, top, 22, SearchRowHeight);
            var next = new Rect(close.x - 25, top, 22, SearchRowHeight);
            var previous = new Rect(next.x - 25, top, 22, SearchRowHeight);
            var countRect = new Rect(previous.x - 51, top, 48, SearchRowHeight);
            var option = new Rect(countRect.x - 108, top, 24, SearchRowHeight);
            var field = new Rect(area.x + 28, top, Mathf.Max(40, option.x - area.x - 34), SearchRowHeight);
            var foldout = new Rect(area.x + 6, top + 2, 18, SearchRowHeight - 2);
            _showReplace = EditorGUI.Foldout(foldout, _showReplace, new GUIContent("", "Show or hide Replace."), true);
            GUI.SetNextControlName(SearchControl);
            _search = GUI.TextField(field, _search, EditorStyles.textField);
            DrawSearchFieldHint(field, _search, "Find", "Find cell text and formulas. Enter / Shift+Enter or F3 / Shift+F3 navigates matching cells.");
            if (current.type == EventType.Repaint)
            {
                _searchAreaRect = area;
                _findInputRect = field;
                _replaceInputRect = Rect.zero;
                _searchFoldoutRect = foldout;
            }
            _matchCase = DrawSearchOption(option, _matchCase, "Aa", "Match Case", 0);
            option.x += 27;
            _wholeWord = DrawSearchOption(option, _wholeWord, "ab", "Match Whole Word (Unicode word boundaries)", 1);
            option.x += 27;
            _useRegex = DrawSearchOption(option, _useRegex, ".*", "Use Regular Expression (.NET Regex)", 2);
            option.x += 27;
            bool selection = DrawSearchOption(option, _searchSelection, "≡", "Find in Selection. Uses the selected range when enabled.", 3);
            if (selection != _searchSelection)
            {
                _searchSelection = selection;
                _searchRange = new RectInt(MinColumn, MinRow, MaxColumn - MinColumn + 1, MaxRow - MinRow + 1);
            }
            RefreshSearch();
            int selected = _searchMatches.IndexOf(_selected);
            string count = _searchMatches.Count == 0 ? "No results" : selected >= 0 ? $"{selected + 1}/{_searchMatches.Count}" : _searchMatches.Count.ToString();
            GUI.Label(countRect, new GUIContent(count, "Matching cells. Multiple occurrences inside one cell count as one result."), EditorStyles.centeredGreyMiniLabel);
            using (new EditorGUI.DisabledScope(_searchMatches.Count == 0))
            {
                if (GUI.Button(previous, new GUIContent("↑", "Previous match (Shift+Enter / Shift+F3)"), _searchActionStyle))
                {
                    FindMatch(true);
                }
                if (GUI.Button(next, new GUIContent("↓", "Next match (Enter / F3)"), _searchActionStyle))
                {
                    FindNext();
                }
            }
            if (GUI.Button(close, new GUIContent("×", "Close Find (Esc). Ctrl/Cmd+F opens it again."), _searchActionStyle))
            {
                CloseSearch();
            }
            float bottom = field.yMax;
            if (_showReplace)
            {
                float replaceTop = top + SearchRowHeight + 3;
                var all = new Rect(area.xMax - 76, replaceTop, 70, SearchRowHeight);
                var replace = new Rect(all.x - 63, replaceTop, 60, SearchRowHeight);
                field = new Rect(field.x, replaceTop, Mathf.Max(40, replace.x - field.x - 6), SearchRowHeight);
                GUI.SetNextControlName(ReplaceControl);
                _replacement = GUI.TextField(field, _replacement, EditorStyles.textField);
                DrawSearchFieldHint(field, _replacement, "Replace", "Replacement text. Regex mode supports $1 and ${name}; plain text mode keeps dollar signs literally.");
                if (current.type == EventType.Repaint)
                {
                    _replaceInputRect = field;
                }
                using (new EditorGUI.DisabledScope(!_writable || _searchMatches.Count == 0))
                {
                    if (GUI.Button(replace, new GUIContent("Replace", "Replace the current matching cell, then move to the next result."), _searchActionStyle))
                    {
                        ReplaceMatches(false);
                    }
                    if (GUI.Button(all, new GUIContent("Replace All", "Replace all matching cells in one Undo step."), _searchActionStyle))
                    {
                        ReplaceMatches(true);
                    }
                }
                bottom = field.yMax;
            }
            if (_searchError != null)
            {
                EditorGUI.HelpBox(new Rect(area.x + 6, bottom + 4, area.width - 12, Mathf.Max(40, area.yMax - bottom - 8)), _searchError, MessageType.Error);
            }
            if (EditorGUI.EndChangeCheck())
            {
                RefreshSearch();
                Repaint();
            }
            if (_focusSearch && current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(SearchControl);
                _focusSearch = false;
            }
            string focus = GUI.GetNameOfFocusedControl();
            if (focus == SearchControl)
            {
                _findKeyboardControl = GUIUtility.keyboardControl;
            }
            else if (focus == ReplaceControl)
            {
                _replaceKeyboardControl = GUIUtility.keyboardControl;
            }
        }

        // 查找控件晚于网格绘制，因此用上一轮登记的键盘 ID 识别焦点，不在登记名称之前查询它。
        private bool HandleSearchKeyboard(Event current)
        {
            bool replace = _replaceKeyboardControl != 0 && GUIUtility.keyboardControl == _replaceKeyboardControl;
            if (!HasSearchTextFocus)
            {
                return false;
            }
            if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                if (replace && !current.shift)
                {
                    ReplaceMatches(false);
                }
                else
                {
                    FindMatch(current.shift);
                }
            }
            else if (current.keyCode == KeyCode.Escape)
            {
                CloseSearch();
            }
            else
            {
                return false;
            }
            current.Use();
            Repaint();
            return true;
        }

        private void DrawSearchFieldHint(Rect rect, string value, string placeholder, string tooltip)
        {
            if (value.Length == 0)
            {
                var hint = new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height);
                GUI.Label(hint, placeholder, _searchHintStyle);
            }
            GUI.Label(rect, new GUIContent("", tooltip));
        }

        // 原生按钮负责边框和交互，只给选中条件加淡色；保留圆角，不再额外覆盖实心矩形。
        private bool DrawSearchOption(Rect rect, bool value, string label, string tooltip, int index)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _searchOptionRects[index] = rect;
            }
            Color previous = GUI.backgroundColor;
            if (value)
            {
                GUI.backgroundColor *= Color.Lerp(Color.white, Accent, .3f);
            }
            _searchOptionStyle.fontStyle = value ? FontStyle.Bold : FontStyle.Normal;
            // 普通按钮不会套用 Toggle 的蓝色选中贴图；点击时自行翻转条件并通知外层刷新搜索。
            bool clicked = GUI.Button(rect, new GUIContent(label, tooltip), _searchOptionStyle);
            GUI.backgroundColor = previous;
            if (clicked)
            {
                GUI.changed = true;
            }
            return clicked ? !value : value;
        }

        private void CloseSearch()
        {
            _showSearch = false;
            RefreshSearch();
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
        }
    }
}
