using System;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    // 标签独立于网格的横向滚动；先取得固定视口，再绘制可滚动内容，避免所有标签把 Inspector 撑宽。
    public partial class SpreadsheetInspector
    {
        private const float SheetTabHeight = 26; // 标签与导航按钮的高度。
        private const float SheetNavigationWidth = 24; // 两个方向按钮及列表按钮各自预留的宽度。
        private Rect _sheetTabsRect; // 底部标签栏的完整区域；CSV 不占用此区域。
        private Rect _sheetViewportRect; // 标签滚动区，不包含左右切换和完整列表按钮。
        private float[] _sheetTabEdges = Array.Empty<float>(); // 标签前缀坐标，长名称按实际宽度计算并限制上限。
        private int _visibleSheetIndex = -1; // 上次定位的活动 Sheet；手动滚动标签时不强制跳回。

        private void DrawSheetTabs(Rect area)
        {
            if (_document.IsCsv)
            {
                _sheetTabsRect = _sheetViewportRect = Rect.zero;
                return;
            }

            _sheetTabEdges = new float[_document.SheetNames.Length + 1];
            for (int index = 0; index < _document.SheetNames.Length; index++)
            {
                float width = Mathf.Clamp(_headerStyle.CalcSize(new GUIContent(_document.SheetNames[index])).x + 24, 80, 180);
                _sheetTabEdges[index + 1] = _sheetTabEdges[index] + width + 2;
            }

            // GUI.BeginScrollView 使用明确的矩形裁切，不让标签的总宽度参与 GUILayout 的最小宽度计算。
            if (Event.current.type == EventType.Repaint)
            {
                _sheetTabsRect = area;
            }
            var viewport = new Rect(area.x + SheetNavigationWidth * 2, area.y,
                Mathf.Max(1, area.width - SheetNavigationWidth * 3), area.height);
            EditorGUI.DrawRect(area, HeaderBackground);
            using (new EditorGUI.DisabledScope(_document.SheetIndex == 0))
            {
                if (DrawHeaderButton(new Rect(area.x, area.y, SheetNavigationWidth - 1, SheetTabHeight), "‹", true, false,
                    "Previous worksheet. The active tab is brought into view."))
                {
                    SwitchSheet(_document.SheetIndex - 1);
                }
            }
            using (new EditorGUI.DisabledScope(_document.SheetIndex == _document.SheetNames.Length - 1))
            {
                if (DrawHeaderButton(new Rect(area.x + SheetNavigationWidth, area.y, SheetNavigationWidth - 1, SheetTabHeight), "›", true, false,
                    "Next worksheet. The active tab is brought into view."))
                {
                    SwitchSheet(_document.SheetIndex + 1);
                }
            }
            var list = new Rect(area.xMax - SheetNavigationWidth, area.y, SheetNavigationWidth, SheetTabHeight);
            if (DrawHeaderButton(list, "▾", true, false, "List all worksheets and jump directly to one."))
            {
                var menu = new GenericMenu();
                for (int index = 0; index < _document.SheetNames.Length; index++)
                {
                    int sheet = index;
                    menu.AddItem(new GUIContent(_document.SheetNames[index]), index == _document.SheetIndex, () => SwitchSheet(sheet));
                }
                menu.DropDown(list);
            }

            float contentWidth = _sheetTabEdges[_document.SheetNames.Length];
            // 换 Sheet、撤销跨 Sheet 或缩放窗口时定位活动标签；普通重绘不会打断用户浏览其他标签。
            Event current = Event.current;
            if (current.type != EventType.Layout && (_visibleSheetIndex != _document.SheetIndex
                || !Mathf.Approximately(_sheetViewportRect.width, viewport.width)))
            {
                float left = _sheetTabEdges[_document.SheetIndex];
                float right = _sheetTabEdges[_document.SheetIndex + 1];
                _sheetScroll.x = Mathf.Clamp(_sheetScroll.x, right - viewport.width, left);
                _visibleSheetIndex = _document.SheetIndex;
            }
            if (current.type == EventType.Repaint)
            {
                _sheetViewportRect = viewport;
            }
            if (current.type == EventType.ScrollWheel && viewport.Contains(current.mousePosition))
            {
                _sheetScroll.x += (current.delta.x + current.delta.y) * 24;
                current.Use();
            }
            if (current.type != EventType.Layout)
            {
                _sheetScroll = new Vector2(Mathf.Clamp(_sheetScroll.x, 0, Mathf.Max(0, contentWidth - viewport.width)), 0);
            }
            _sheetScroll = GUI.BeginScrollView(viewport, _sheetScroll, new Rect(0, 0, contentWidth, SheetTabHeight),
                false, false, GUI.skin.horizontalScrollbar, GUIStyle.none);
            for (int index = 0; index < _document.SheetNames.Length; index++)
            {
                var tab = new Rect(_sheetTabEdges[index], 0, _sheetTabEdges[index + 1] - _sheetTabEdges[index] - 2, SheetTabHeight);
                bool active = index == _document.SheetIndex;
                if (DrawHeaderButton(tab, _document.SheetNames[index], true, active,
                    _document.SheetNames[index] + "\nClick to switch. Use the mouse wheel or scrollbar to browse tabs.") && !active)
                {
                    SwitchSheet(index);
                }
                if (active)
                {
                    EditorGUI.DrawRect(new Rect(tab.x, tab.yMax - 3, tab.width, 3), Accent);
                }
            }
            GUI.EndScrollView();
        }

        private void SwitchSheet(int index)
        {
            Run(() =>
            {
                CommitEdit();
                _editingInline = false;
                StoreViewLayout();
                _document.SheetIndex = index;
                _rows = _columns = 0;
                RebuildGrid();
                Select(0, 0, false);
                SessionState.SetInt(_layoutKey + ".sheet", index);
                StoreDraft();
                RestoreSheetView();
                StoreViewLayout();
            });
        }
    }
}
