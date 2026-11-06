using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    // 鼠标选择使用固定视口坐标，离开数据区后仍能拖选，不依赖某个可见单元格接收事件。
    public partial class SpreadsheetInspector
    {
        private int _selectionControl; // 拖选时持有鼠标的控件 ID；0 表示没有拖选。
        private bool _selectionDragging; // 鼠标开始移动后才自动滚动，单击或长按格子不触发。
        private Vector2 _selectionPointer; // 鼠标相对于数据视口左上角的位置，可以超出视口。
        private double _selectionScrollTime; // 上一次滚动的编辑器时间，用于按秒计算滚动距离。

        // 先在数据视口判断点击，再将鼠标位置映射到已有的行列前缀坐标。
        private void HandleGridMouse(Rect viewport, int control)
        {
            Event current = Event.current;
            if (_selectionControl != 0)
            {
                if (GUIUtility.hotControl != _selectionControl || current.rawType == EventType.Ignore)
                {
                    EndSelectionDrag();
                    return;
                }
                if (current.type == EventType.MouseDrag && current.button == 0)
                {
                    _selectionDragging = true;
                    _selectionPointer = current.mousePosition - viewport.position;
                    ExtendDragSelection(viewport.size);
                    current.Use();
                    Repaint();
                }
                else if (current.rawType == EventType.MouseUp && current.button == 0)
                {
                    EndSelectionDrag();
                    current.Use();
                    Repaint();
                }
                else if (current.type == EventType.Repaint && _selectionDragging)
                {
                    ScrollDragSelection(viewport.size);
                }
                return;
            }

            // 滚动条、尺寸拖动条和文本框先持有鼠标时，不从它们的拖动创建选区。
            if (GUIUtility.hotControl != 0 || current.type != EventType.MouseDown
                || (current.button != 0 && current.button != 1) || !viewport.Contains(current.mousePosition))
            {
                return;
            }
            Vector2 content = current.mousePosition - viewport.position + _scroll;
            if (content.x >= _columnEdges[_columns] || content.y >= _rowEdges[_rows])
            {
                return;
            }
            int row = FindIndex(_rowEdges, content.y);
            int column = FindIndex(_columnEdges, content.x);
            if (_editingInline && _selected == new Vector2Int(column, row))
            {
                // 正在编辑的格子由 TextArea 处理光标定位和文字选择。
                return;
            }
            bool inSelection = row >= MinRow && row <= MaxRow && column >= MinColumn && column <= MaxColumn;
            bool doubleClick = current.button == 0 && current.clickCount == 2;
            Run(() =>
            {
                if (!(current.button == 1 && inSelection))
                {
                    Select(row, column, current.shift);
                }
                _editingInline = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = control;
                EditorGUIUtility.editingTextField = false;
                if (current.button == 1)
                {
                    ShowCellMenu();
                }
                else if (doubleClick && _writable)
                {
                    BeginInlineEdit();
                }
                else
                {
                    _selectionControl = GUIUtility.hotControl = control;
                    _selectionDragging = false;
                    _selectionPointer = current.mousePosition - viewport.position;
                    _selectionScrollTime = EditorApplication.timeSinceStartup;
                    EditorApplication.update += RepaintSelectionDrag;
                }
            });
            current.Use();
            Repaint();
        }

        private void RepaintSelectionDrag()
        {
            // InspectorElement 也可以嵌在其他窗口里，直接标记所属容器，确保它同样收到重绘。
            _inspectorElement?.MarkDirtyRepaint();
            Repaint();
        }

        // Editor 更新只请求重绘；滚动与选区仍在 OnGUI 中修改，鼠标停住时也会继续推进。
        private void ScrollDragSelection(Vector2 viewport)
        {
            double now = EditorApplication.timeSinceStartup;
            float seconds = Mathf.Min((float)(now - _selectionScrollTime), 0.1f);
            _selectionScrollTime = now;
            var velocity = new Vector2(SelectionScrollSpeed(_selectionPointer.x, viewport.x),
                SelectionScrollSpeed(_selectionPointer.y, viewport.y));
            EnsurePreviewAt(_scroll + velocity * seconds + viewport);
            _scroll = new Vector2(
                Mathf.Clamp(_scroll.x + velocity.x * seconds, 0, Mathf.Max(0, _columnEdges[_columns] - viewport.x)),
                Mathf.Clamp(_scroll.y + velocity.y * seconds, 0, Mathf.Max(0, _rowEdges[_rows] - viewport.y)));
            ExtendDragSelection(viewport);
        }

        private void ExtendDragSelection(Vector2 viewport)
        {
            // 超出视口时使用最近的可见边缘；滚动后同一鼠标位置会对应更远的行列。
            int row = FindIndex(_rowEdges, _scroll.y + Mathf.Clamp(_selectionPointer.y, 0, viewport.y - 1));
            int column = FindIndex(_columnEdges, _scroll.x + Mathf.Clamp(_selectionPointer.x, 0, viewport.x - 1));
            if (_selected != new Vector2Int(column, row))
            {
                Run(() => Select(row, column, true));
            }
        }

        private static float SelectionScrollSpeed(float pointer, float size)
        {
            // 越过边缘越远，滚动越快；两个轴独立计算，因此四角也可以斜向滚动。
            float scrollEdge = SpreadsheetSettings.instance.ScrollEdge;
            float edge = Mathf.Min(scrollEdge, size / 2);
            float distance = pointer < edge ? pointer - edge : pointer > size - edge ? pointer - (size - edge) : 0;
            return Mathf.Clamp(distance, -scrollEdge * 4, scrollEdge * 4) * 18 * SpreadsheetSettings.instance.ScrollSpeed;
        }

        private void EndSelectionDrag()
        {
            if (_selectionControl == 0)
            {
                return;
            }
            if (GUIUtility.hotControl == _selectionControl)
            {
                GUIUtility.hotControl = 0;
            }
            _selectionControl = 0;
            EditorApplication.update -= RepaintSelectionDrag;
        }
    }
}
