using System.Collections;
using ClosedXML.Excel;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace XlsxInspector.Tests
{
    public partial class SpreadsheetInspectorTests
    {
        /// <summary>多张长名称 Sheet 在窄 Inspector 中保持裁切，可滚动、点击导航，并定位最后一张标签。</summary>
        [UnityTest]
        public IEnumerator ManyWorksheetTabsStayWithinTheInspectorAndRemainNavigable()
        {
            using (var workbook = new XLWorkbook())
            {
                for (int index = 0; index < 24; index++)
                {
                    workbook.AddWorksheet("Worksheet " + (index + 1) + " - Configuration").Cell("A1").Value = index;
                }
                workbook.SaveAs(_xlsxPath);
            }
            AssetDatabase.ImportAsset(_xlsxPath);
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                Open(_xlsxPath);
                window.ShowUtility();
                window.position = new Rect(40, 40, 560, 800);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Rect bar = GetField<Rect>("_sheetTabsRect");
                Rect viewport = GetField<Rect>("_sheetViewportRect");
                Assert.That(bar.width, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.width));
                Assert.That(viewport.width, Is.GreaterThan(200));
                Assert.That(inspector.worldBound.width, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.width + 1));
                AssertPanelFits(scroll, inspector);

                // 滚轮只浏览标签，不切 Sheet；后续重绘不能把标签滚动位置强行复原。
                Vector2 pointer = inspector.worldBound.position + viewport.center;
                window.SendEvent(new Event { type = EventType.ScrollWheel, delta = new Vector2(0, 12), mousePosition = pointer });
                yield return SettleLayout(window);
                Assert.That(GetField<Vector2>("_sheetScroll").x, Is.GreaterThan(100));
                Assert.That(Document.SheetIndex, Is.Zero);

                Invoke("SwitchSheet", 23);
                yield return SettleLayout(window);
                AssertActiveWorksheetTabIsVisible();
                Assert.That(Document.GetText(0, 0), Is.EqualTo("23"));
                bar = GetField<Rect>("_sheetTabsRect");
                Vector2 previous = inspector.worldBound.position + bar.position + new Vector2(12, 13);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = previous });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = previous });
                yield return SettleLayout(window);
                Assert.That(Document.SheetIndex, Is.EqualTo(22));
                AssertActiveWorksheetTabIsVisible();

                window.position = new Rect(40, 40, 440, 800);
                yield return SettleLayout(window);
                AssertActiveWorksheetTabIsVisible();
                // 调整面板高度会改变可见行号数量，页签滚动位置和底部占用不能随控件 ID 变化跳动。
                bar = GetField<Rect>("_sheetTabsRect");
                float sheetOffset = GetField<Vector2>("_sheetScroll").x;
                float footer = GetField<float>("_gridFooterHeight");
                Rect grid = GetField<Rect>("_gridRect");
                Vector2 handle = inspector.worldBound.position + new Vector2(grid.center.x, grid.yMax + 4);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = handle });
                Assert.That(GetField<bool>("_resizingPanel"), Is.True);
                for (int step = 0; step < 10; step++)
                {
                    window.SendEvent(new Event
                    {
                        type = EventType.MouseDrag,
                        button = 0,
                        mousePosition = handle + new Vector2(0, -step * 11)
                    });
                    yield return SettleLayout(window);
                    Assert.That(GetField<Vector2>("_sheetScroll").x, Is.EqualTo(sheetOffset).Within(1));
                    Assert.That(GetField<float>("_gridFooterHeight"), Is.EqualTo(footer).Within(1));
                    Rect settledTabs = GetField<Rect>("_sheetTabsRect");
                    for (int frame = 0; frame < 3; frame++)
                    {
                        window.Repaint();
                        yield return null;
                        Assert.That(GetField<Rect>("_sheetTabsRect").y, Is.EqualTo(settledTabs.y).Within(1));
                    }
                }
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = handle + new Vector2(0, -99) });
                Assert.That(Document.IsDirty, Is.False);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>CSV 不创建标签栏，并把原来的底部占用还给网格。</summary>
        [UnityTest]
        public IEnumerator CsvHidesTheWorksheetBarAndUsesTheFreedSpace()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                Open(_xlsxPath);
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                window.rootVisualElement.Add(scroll);
                scroll.Add(new InspectorElement(_editor));
                yield return SettleLayout(window);
                float xlsxFooter = GetField<float>("_gridFooterHeight");
                Assert.That(GetField<Rect>("_sheetTabsRect").height, Is.GreaterThan(0));

                scroll.Clear();
                Open(_csvPath);
                scroll.Add(new InspectorElement(_editor));
                yield return SettleLayout(window);
                Assert.That(GetField<Rect>("_sheetTabsRect"), Is.EqualTo(Rect.zero));
                Assert.That(GetField<Rect>("_sheetViewportRect"), Is.EqualTo(Rect.zero));
                Assert.That(GetField<float>("_gridFooterHeight"), Is.LessThan(xlsxFooter - 30));
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                AssertPanelFits(scroll, inspector);
            }
            finally
            {
                window.Close();
            }
        }

        private void AssertActiveWorksheetTabIsVisible()
        {
            float[] edges = GetField<float[]>("_sheetTabEdges");
            float offset = GetField<Vector2>("_sheetScroll").x;
            float width = GetField<Rect>("_sheetViewportRect").width;
            Assert.That(edges[Document.SheetIndex], Is.GreaterThanOrEqualTo(offset - 1));
            Assert.That(edges[Document.SheetIndex + 1], Is.LessThanOrEqualTo(offset + width + 1));
        }
    }
}
