using System;
using System.Collections;
using System.IO;
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
        /// <summary>空白导航不受初始预览范围限制，且不增加文件内容、草稿或 CSV 格式数组。</summary>
        /// <param name="excel">为 true 时验证 Excel，否则验证 CSV。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void EmptyKeyboardNavigationExtendsPreviewWithoutEditingTheDocument(bool excel)
        {
            string path = excel ? _xlsxPath : _csvPath;
            byte[] original = File.ReadAllBytes(path);
            Open(path);
            int height = Document.RowCount;
            int width = Document.ColumnCount;
            int row = GetField<int>("_rows") - 1;
            int column = GetField<int>("_columns") - 1;
            Invoke("Select", row, column, false);
            Invoke("MoveSelection", 1, 1, false);
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(column + 1, row + 1)));
            Assert.That(Document.RowCount, Is.EqualTo(height));
            Assert.That(Document.ColumnCount, Is.EqualTo(width));
            Assert.That(Document.IsDirty, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            if (!excel)
            {
                string json = JsonUtility.ToJson(GetField<object>("_csvLayout"));
                Assert.That(json, Does.Contain("\"columnWidths\":[]"));
                Assert.That(json, Does.Contain("\"rowHeights\":[]"));
            }
        }

        /// <summary>Excel 的最后一行列仍是边界；预览可到达它们，但不为百万行创建文档行对象。</summary>
        [Test]
        public void PreviewRespectsExcelLimitsWithoutAllocatingWorkbookRows()
        {
            Open(_xlsxPath);
            Invoke("EnsurePreviewCell", Document.MaxRows - 1, Document.MaxColumns - 1);
            Invoke("Select", Document.MaxRows - 1, Document.MaxColumns - 1, false);
            Invoke("MoveSelection", 1, 1, false);
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(Document.MaxColumns - 1, Document.MaxRows - 1)));
            Assert.That(GetField<int>("_rows"), Is.EqualTo(Document.MaxRows));
            Assert.That(GetField<int>("_columns"), Is.EqualTo(Document.MaxColumns));
            Assert.That(Document.ReadDimensions().Rows.Count, Is.LessThan(10));
            Assert.That(Document.ReadDimensions().Columns.Count, Is.LessThan(10));
            Assert.That(Document.RowCount, Is.EqualTo(1));
            Assert.That(Document.ColumnCount, Is.EqualTo(1));
            Assert.That(Document.IsDirty, Is.False);
        }

        /// <summary>远处空白可以释放，内容和选区坐标保持；显式行列尺寸仍可在再次定位后恢复。</summary>
        /// <param name="excel">为 true 时验证 Excel，否则验证 CSV。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void UnusedPreviewShrinksWithoutLosingSelectionContentOrDimensions(bool excel)
        {
            Open(excel ? _xlsxPath : _csvPath);
            SetField("_viewportSize", new Vector2(600, 300));
            Invoke("EnsurePreviewCell", 999, 249);
            Invoke("Select", 999, 249, false);
            Invoke("ResizeSelection", 177f, 33f);
            SetField("_scroll", Vector2.zero);
            Invoke("ReleaseUnusedPreview");
            Assert.That(GetField<int>("_rows"), Is.LessThan(100));
            Assert.That(GetField<int>("_columns"), Is.LessThan(30));
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(249, 999)));
            Invoke("RevealSelection");
            Assert.That(GetField<int>("_rows"), Is.GreaterThanOrEqualTo(1000));
            Assert.That((float)Invoke("ColumnWidth", 249), Is.EqualTo(177).Within(.1f));
            Assert.That((float)Invoke("RowHeight", 999), Is.EqualTo(33).Within(.1f));

            // 只要远处有内容，回到左上角也必须保留该行列；清空后才允许回收预览。
            Invoke("Change", (Action)(() => Document.SetText(999, 249, "far content")), "Write distant cell", true);
            SetField("_scroll", Vector2.zero);
            Invoke("ReleaseUnusedPreview");
            Assert.That(GetField<int>("_rows"), Is.GreaterThanOrEqualTo(1000));
            Assert.That(GetField<int>("_columns"), Is.GreaterThanOrEqualTo(250));
            Assert.That(Document.GetText(999, 249), Is.EqualTo("far content"));
            Invoke("Change", (Action)(() => Document.SetText(999, 249, "")), "Clear distant cell", true);
            Invoke("ReleaseUnusedPreview");
            Assert.That(GetField<int>("_rows"), Is.LessThan(100));
            Assert.That(GetField<int>("_columns"), Is.LessThan(30));
        }

        /// <summary>真实拖动滚动条返回左上角，按住时范围保持，松开后缩回，滚动条滑块恢复长度。</summary>
        [UnityTest]
        public IEnumerator ScrollbarReturnReleasesOffscreenEmptyCellsAfterMouseUp()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                Open(_xlsxPath);
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                Invoke("EnsurePreviewCell", 999, 249);
                Invoke("Select", 999, 249, false);
                Invoke("RevealSelection");
                yield return SettleLayout(window);
                int expandedRows = GetField<int>("_rows");
                int expandedColumns = GetField<int>("_columns");
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Rect grid = GetField<Rect>("_gridRect");
                // 滑块不一定在最右端：视口后还留有一屏空格，需按内容长度和当前滚动位置定位。
                Vector2 visible = GetField<Vector2>("_viewportSize");
                float width = GetField<float[]>("_columnEdges")[expandedColumns];
                float horizontalTrack = visible.x - 32;
                float horizontalThumb = Mathf.Max(10, horizontalTrack * visible.x / width);
                float horizontalX = grid.x + 45 + 16 + horizontalThumb / 2
                    + (horizontalTrack - horizontalThumb) * GetField<Vector2>("_scroll").x / (width - visible.x);
                Vector2 horizontal = inspector.worldBound.position + new Vector2(horizontalX, grid.yMax - 8);
                Vector2 left = inspector.worldBound.position + new Vector2(grid.x + 45 + 20, grid.yMax - 8);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = horizontal });
                Assert.That(GUIUtility.hotControl, Is.Not.Zero);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = left });
                yield return SettleLayout(window);
                Assert.That(GetField<int>("_columns"), Is.EqualTo(expandedColumns));
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = left });
                yield return SettleLayout(window);
                Assert.That(GetField<Vector2>("_scroll").x, Is.LessThan(10));
                Assert.That(GetField<int>("_columns"), Is.LessThan(30));

                float height = GetField<float[]>("_rowEdges")[expandedRows];
                float verticalTrack = visible.y - 32;
                float verticalThumb = Mathf.Max(10, verticalTrack * visible.y / height);
                float verticalY = grid.y + 24 + 16 + verticalThumb / 2
                    + (verticalTrack - verticalThumb) * GetField<Vector2>("_scroll").y / (height - visible.y);
                Vector2 vertical = inspector.worldBound.position + new Vector2(grid.xMax - 8, verticalY);
                Vector2 top = inspector.worldBound.position + new Vector2(grid.xMax - 8, grid.y + 24 + 20);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = vertical });
                Assert.That(GUIUtility.hotControl, Is.Not.Zero);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = top });
                yield return SettleLayout(window);
                Assert.That(GetField<int>("_rows"), Is.EqualTo(expandedRows));
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = top });
                yield return SettleLayout(window);
                Assert.That(GetField<Vector2>("_scroll").y, Is.LessThan(10));
                Assert.That(GetField<int>("_rows"), Is.LessThan(100));
                Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(249, 999)));
                Assert.That(Document.IsDirty, Is.False);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>实际窗口中滚动和持续拖选越过最初空白范围，随后可以直接粘贴并撤销。</summary>
        [UnityTest]
        public IEnumerator EmptyPreviewExtendsForScrollingDraggingAndPasting()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                Open(_csvPath);
                byte[] original = File.ReadAllBytes(_csvPath);
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                int initialRows = GetField<int>("_rows");
                int initialColumns = GetField<int>("_columns");
                Vector2 size = GetField<Vector2>("_viewportSize");
                float[] rowEdges = GetField<float[]>("_rowEdges");
                float[] columnEdges = GetField<float[]>("_columnEdges");
                SetField("_scroll", new Vector2(columnEdges[initialColumns] - size.x - 1, rowEdges[initialRows] - size.y - 1));
                yield return SettleLayout(window);
                Assert.That(GetField<int>("_rows"), Is.GreaterThan(initialRows));
                Assert.That(GetField<int>("_columns"), Is.GreaterThan(initialColumns));

                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Rect viewport = SelectionViewport(inspector);
                Vector2 start = viewport.center;
                Vector2 outside = viewport.max + new Vector2(90, 90);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = outside });
                yield return new WaitForSecondsRealtime(.35f);
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = outside });
                Vector2Int selected = GetField<Vector2Int>("_selected");
                Assert.That(selected.y, Is.GreaterThanOrEqualTo(initialRows));
                Assert.That(Document.RowCount, Is.EqualTo(2));
                Assert.That(Document.ColumnCount, Is.EqualTo(2));
                Assert.That(Document.IsDirty, Is.False);
                Assert.That(File.ReadAllBytes(_csvPath), Is.EqualTo(original));

                Invoke("Change", (Action)(() => Document.Paste(selected.y, selected.x, "a\tb\nc\td")), "Paste into preview", true);
                Assert.That(Document.GetText(selected.y + 1, selected.x + 1), Is.EqualTo("d"));
                Assert.That(GetField<int>("_dataRows"), Is.EqualTo(selected.y + 2));
                Undo.PerformUndo();
                Assert.That(GetField<int>("_dataRows"), Is.EqualTo(2));
                Assert.That(Document.GetText(selected.y, selected.x), Is.Empty);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
