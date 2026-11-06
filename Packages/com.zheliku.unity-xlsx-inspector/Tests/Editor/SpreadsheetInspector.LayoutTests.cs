using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace XlsxInspector.Tests
{
    // 通过真实窗口和 UI Toolkit 布局验证可见范围，避免只测试固定数字的 Clamp 而遗漏坐标和滚动问题。
    public partial class SpreadsheetInspectorTests
    {
        private sealed class InspectorLayoutTestWindow : EditorWindow
        {
        }

        /// <summary>验证旧的超大面板自动收回，窗口缩小时重新限高，底部拖动条始终可见且可再次向上拖动。</summary>
        [UnityTest]
        public IEnumerator PanelHeightRecoversAndTracksTheInspectorViewport()
        {
            float savedHeight = SessionState.GetFloat("XlsxInspector.GridHeight", 440);
            SessionState.SetFloat("XlsxInspector.GridHeight", 2000);
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                Open(_xlsxPath);
                window.ShowUtility();
                // ShowUtility 会恢复此窗口类型上次关闭时的位置，测试尺寸必须在显示后设置。
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Assert.That(inspector, Is.Not.Null);

                AssertPanelFits(scroll, inspector);
                Assert.That(scroll.contentViewport.worldBound.height, Is.GreaterThan(600));
                Assert.That(GetField<float>("_gridHeight"), Is.LessThan(2000));
                float originalHeight = GetField<float>("_gridHeight");

                // 向下拖动很远仍然受实际窗口约束，不会把拖动条拉到窗口外面。
                DragPanel(window, inspector, 4000);
                yield return SettleLayout(window);
                AssertPanelFits(scroll, inspector);

                window.position = new Rect(40, 40, 800, 520);
                yield return SettleLayout(window);
                AssertPanelFits(scroll, inspector);
                Assert.That(GetField<float>("_gridHeight"), Is.LessThan(originalHeight));

                float beforeDrag = GetField<float>("_gridHeight");
                DragPanel(window, inspector, -80);
                yield return SettleLayout(window);
                Assert.That(GetField<float>("_gridHeight"), Is.LessThan(beforeDrag));
                AssertPanelFits(scroll, inspector);
            }
            finally
            {
                window.Close();
                SessionState.SetFloat("XlsxInspector.GridHeight", savedHeight);
            }
        }

        /// <summary>真实点击左上角全选按钮，复制结果应包含全部已有数据；横向与纵向滚动均不改变按钮的位置。</summary>
        [UnityTest]
        public IEnumerator CornerButtonSelectsAllData()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                Open(_csvPath);
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Assert.That(inspector, Is.Not.Null);

                SetField("_anchor", new Vector2Int(1, 1));
                SetField("_selected", new Vector2Int(1, 1));
                SetField("_scroll", new Vector2(25, 10));
                Rect grid = GetField<Rect>("_gridRect");
                Vector2 corner = inspector.worldBound.position + grid.position + new Vector2(20, 12);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = corner });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = corner });

                Assert.That(GetField<Vector2Int>("_anchor"), Is.EqualTo(Vector2Int.zero));
                Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(1, 1)));
                _editor.Copy();
                Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(Document.Copy(0, 0, 1, 1)));
                Assert.That(Document.IsDirty, Is.False);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>灰色预览格仍可直接粘贴，粘贴扩展实际内容范围，加号仍以当前选区为参照插入。</summary>
        [UnityTest]
        public IEnumerator UnusedPreviewCellsAcceptPasteWithoutPreallocatingRowsOrColumns()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                Open(_csvPath);
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                Assert.That(Document.RowCount, Is.EqualTo(2));
                Assert.That(Document.ColumnCount, Is.EqualTo(2));
                Assert.That(GetField<int>("_rows"), Is.GreaterThan(Document.RowCount));
                Assert.That(GetField<int>("_columns"), Is.GreaterThan(Document.ColumnCount));
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Rect grid = GetField<Rect>("_gridRect");
                Vector2 cell = inspector.worldBound.position + grid.position + new Vector2(45 + 2 * 120 + 20, 24 + 3 * 24 + 10);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = cell });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = cell });
                Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(2, 3)));
                EditorGUIUtility.systemCopyBuffer = "tail\tend\nsecond\trow";
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.V, control = true });
                Assert.That(Document.GetText(3, 2), Is.EqualTo("tail"));
                Assert.That(Document.GetText(4, 3), Is.EqualTo("row"));
                Assert.That(Document.RowCount, Is.EqualTo(5));
                Assert.That(Document.ColumnCount, Is.EqualTo(4));
                ClickInsertButton(window, inspector, true);
                Assert.That(Document.RowCount, Is.EqualTo(6));
                Assert.That(GetField<Vector2Int>("_selected").y, Is.EqualTo(4));
                Assert.That(Document.GetText(5, 3), Is.EqualTo("row"));
                ClickInsertButton(window, inspector, false);
                Assert.That(Document.ColumnCount, Is.EqualTo(5));
                Assert.That(GetField<Vector2Int>("_selected").x, Is.EqualTo(4));
            }
            finally
            {
                window.Close();
            }
        }

        private static IEnumerator SettleLayout(EditorWindow window)
        {
            // UI Toolkit 先计算父视口，IMGUI 再记录工具栏和尾部位置，最后一次布局应用新的高度上限。
            for (int frame = 0; frame < 8; frame++)
            {
                window.Repaint();
                yield return null;
            }
        }

        private void AssertPanelFits(ScrollView scroll, VisualElement inspector)
        {
            Assert.That(scroll.contentViewport.worldBound.height, Is.GreaterThan(0));
            Rect grid = GetField<Rect>("_gridRect");
            float footer = GetField<float>("_gridFooterHeight");
            float bottom = inspector.worldBound.yMin + grid.yMax + footer;
            Assert.That(bottom, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.yMax + 1),
                "网格、拖动条、Sheet 标签和状态文字必须一起位于 Inspector 可见范围内。");
        }

        private void DragPanel(EditorWindow window, VisualElement inspector, float distance)
        {
            Rect grid = GetField<Rect>("_gridRect");
            Vector2 start = inspector.worldBound.position + new Vector2(grid.center.x, grid.yMax + 4);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
            Assert.That(GetField<bool>("_resizingPanel"), Is.True, "MouseDown must reach the resize handle at " + start);
            window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + Vector2.up * distance });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = start + Vector2.up * distance });
            Assert.That(GetField<bool>("_resizingPanel"), Is.False, "MouseUp must release the resize handle.");
        }
    }
}
