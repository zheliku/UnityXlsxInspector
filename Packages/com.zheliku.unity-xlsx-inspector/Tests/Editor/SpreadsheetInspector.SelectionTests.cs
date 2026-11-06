using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace XlsxInspector.Tests
{
    // 向真实 Inspector 发送鼠标事件，并跨帧等待重绘，验证鼠标停住后仍持续滚动。
    public partial class SpreadsheetInspectorTests
    {
        /// <summary>四边拖选会持续滚动，保持选区起点，松开鼠标或关闭 Inspector 后停止。</summary>
        [UnityTest]
        public IEnumerator DragSelectionScrollsInAllDirectionsAndStopsOnRelease()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            var settings = SpreadsheetSettings.instance;
            string savedSettings = JsonUtility.ToJson(settings);
            try
            {
                OpenLargeSelectionTable();
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Rect viewport = SelectionViewport(inspector);
                SetField("_scroll", new Vector2(300, 300));
                window.Repaint();
                yield return null;
                Vector2 start = viewport.position + new Vector2(80, 70);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Vector2Int anchor = GetField<Vector2Int>("_anchor");
                Assert.That(GetField<int>("_selectionControl"), Is.Not.Zero);

                // 四个目标分别位于右、左、下、上边缘之外，每次只要求一个轴滚动。
                var targets = new[]
                {
                    new Vector2(viewport.xMax + 70, viewport.center.y),
                    new Vector2(viewport.xMin - 70, viewport.center.y),
                    new Vector2(viewport.center.x, viewport.yMax + 70),
                    new Vector2(viewport.center.x, viewport.yMin - 70)
                };
                for (int direction = 0; direction < targets.Length; direction++)
                {
                    Vector2 before = GetField<Vector2>("_scroll");
                    window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = targets[direction] });
                    yield return new WaitForSecondsRealtime(0.18f);
                    Vector2 after = GetField<Vector2>("_scroll");
                    bool horizontal = direction < 2;
                    float distance = horizontal ? after.x - before.x : after.y - before.y;
                    Assert.That(direction % 2 == 0 ? distance : -distance, Is.GreaterThan(0), "Direction " + direction);
                    Assert.That(horizontal ? after.y : after.x, Is.EqualTo(horizontal ? before.y : before.x).Within(1));
                    Assert.That(GetField<Vector2Int>("_anchor"), Is.EqualTo(anchor));
                    if (direction == 0)
                    {
                        // 不发送新的 MouseDrag；只有 Editor 的更新和重绘，选区仍应继续向右推进。
                        Vector2Int selected = GetField<Vector2Int>("_selected");
                        yield return new WaitForSecondsRealtime(0.18f);
                        Assert.That(GetField<Vector2>("_scroll").x, Is.GreaterThan(after.x));
                        Assert.That(GetField<Vector2Int>("_selected").x, Is.GreaterThan(selected.x));

                        // 项目设置设为 0 后，正在进行的拖选也立即停止自动滚动，恢复后继续原有操作。
                        JsonUtility.FromJsonOverwrite("{\"_scrollSpeed\":0}", settings);
                        Vector2 paused = GetField<Vector2>("_scroll");
                        yield return new WaitForSecondsRealtime(0.12f);
                        Assert.That(GetField<Vector2>("_scroll"), Is.EqualTo(paused));
                        JsonUtility.FromJsonOverwrite(savedSettings, settings);
                    }
                }

                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = targets[3] });
                Assert.That(GetField<int>("_selectionControl"), Is.Zero);
                Vector2 stopped = GetField<Vector2>("_scroll");
                Vector2Int selection = GetField<Vector2Int>("_selected");
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.That(GetField<Vector2>("_scroll"), Is.EqualTo(stopped));
                Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(selection));
                Assert.That(Document.IsDirty, Is.False, "拖选只改变视图，不修改文件或创建草稿。");

                // 关闭 Inspector 也必须释放本次捕获，避免留下持续重绘的 Editor 更新订阅。
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(GetField<int>("_selectionControl"), Is.Not.Zero);
                window.Close();
                Assert.That(GetField<int>("_selectionControl"), Is.Zero);
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(savedSettings, settings);
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        /// <summary>仅在边缘按住而不拖动不会滚动；编辑文本和没有从网格开始的拖动不被选区捕获。</summary>
        [UnityTest]
        public IEnumerator SelectionScrollingRequiresADragStartedInTheGrid()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                OpenLargeSelectionTable();
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Rect viewport = SelectionViewport(inspector);
                Vector2 edge = new Vector2(viewport.xMax - 2, viewport.center.y);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = edge });
                Vector2 before = GetField<Vector2>("_scroll");
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.That(GetField<Vector2>("_scroll"), Is.EqualTo(before));
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = edge });

                Vector2Int selected = GetField<Vector2Int>("_selected");
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = viewport.max + Vector2.one * 60 });
                Assert.That(GetField<int>("_selectionControl"), Is.Zero);
                Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(selected));

                Vector2 cell = viewport.position + new Vector2(50, 12);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, clickCount = 2, mousePosition = cell });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = cell });
                yield return SettleLayout(window);
                Assert.That(GetField<bool>("_editingInline"), Is.True);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = cell });
                Assert.That(GetField<int>("_selectionControl"), Is.Zero, "TextArea 应自行处理文字选择。");
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = cell });
            }
            finally
            {
                window.Close();
            }
        }

        private void OpenLargeSelectionTable()
        {
            string text = string.Join("\n", Enumerable.Range(0, 80).Select(row =>
                string.Join(",", Enumerable.Range(0, 30).Select(column => row + ":" + column))));
            File.WriteAllText(_csvPath, text);
            AssetDatabase.ImportAsset(_csvPath);
            Open(_csvPath);
        }

        private Rect SelectionViewport(VisualElement inspector)
        {
            Rect grid = GetField<Rect>("_gridRect");
            // 固定表头宽 45、高 24；数据区域再扣除水平和垂直滚动条的预留空间。
            return new Rect(inspector.worldBound.position + grid.position + new Vector2(45, 24),
                GetField<Vector2>("_viewportSize"));
        }
    }
}
