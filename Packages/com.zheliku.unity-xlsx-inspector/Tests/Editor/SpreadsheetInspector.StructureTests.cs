using System.Collections;
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
        /// <summary>亮色区域只跟随内容：追加空白不扩大，输入扩大，清空自动缩回，撤销恢复。</summary>
        [Test]
        public void HighlightedBoundsFollowContentAndUndoWithoutTrimming()
        {
            Open(_csvPath);
            Invoke("InsertStructure", Document.RowCount, true, 1);
            Invoke("InsertStructure", Document.ColumnCount, false, 1);
            Assert.That(GetField<int>("_dataRows"), Is.EqualTo(2));
            Assert.That(GetField<int>("_dataColumns"), Is.EqualTo(2));
            Invoke("SelectAll");
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(1, 1)));
            Invoke("Select", 2, 2, false);
            SetField("_editBuffer", "tail");
            SetField("_editChanged", true);
            Invoke("CommitEdit");
            Assert.That(GetField<int>("_dataRows"), Is.EqualTo(3));
            Assert.That(GetField<int>("_dataColumns"), Is.EqualTo(3));
            SetField("_editBuffer", "");
            SetField("_editChanged", true);
            Invoke("CommitEdit");
            Assert.That(GetField<int>("_dataRows"), Is.EqualTo(2));
            Assert.That(GetField<int>("_dataColumns"), Is.EqualTo(2));
            Undo.PerformUndo();
            Assert.That(GetField<int>("_dataRows"), Is.EqualTo(3));
            Assert.That(Document.GetText(2, 2), Is.EqualTo("tail"));
        }

        /// <summary>一次批量插入只占一个撤销步骤，重做后空白范围和原有数据一并恢复。</summary>
        /// <param name="excel">是否验证 Excel；为 false 时验证 CSV。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void BatchInsertionIsOneUndoOperation(bool excel)
        {
            Open(excel ? _xlsxPath : _csvPath);
            int height = Document.RowCount;
            int width = Document.ColumnCount;
            string original = Document.GetText(0, 0);
            SetField("_scroll", new Vector2(25, 0));
            Invoke("InsertStructure", 0, true, 3);
            Assert.That(GetField<Vector2>("_scroll").x, Is.EqualTo(25));
            Assert.That(Document.RowCount, Is.EqualTo(height + 3));
            Assert.That(Document.GetText(3, 0), Is.EqualTo(original));
            Undo.PerformUndo();
            Assert.That(Document.RowCount, Is.EqualTo(height));
            Assert.That(Document.GetText(0, 0), Is.EqualTo(original));
            Undo.PerformRedo();
            Assert.That(Document.RowCount, Is.EqualTo(height + 3));
            SetField("_scroll", new Vector2(0, 15));
            Invoke("InsertStructure", 0, false, 2);
            Assert.That(GetField<Vector2>("_scroll").y, Is.EqualTo(15));
            Assert.That(Document.ColumnCount, Is.EqualTo(width + 2));
            Assert.That(Document.GetText(3, 2), Is.EqualTo(original));
            Undo.PerformUndo();
            Assert.That(Document.ColumnCount, Is.EqualTo(width));
            Assert.That(Document.GetText(3, 0), Is.EqualTo(original));
        }

        /// <summary>CSV 的个人显示对齐、行高和列宽随批量插入整体移动，并可一次撤销。</summary>
        [Test]
        public void BatchInsertionMovesCsvDisplayFormatting()
        {
            Open(_csvPath);
            Invoke("ResizeSelection", 190f, 38f);
            Invoke("ApplyFormat", HorizontalCellAlignment.Center, VerticalCellAlignment.Center, true);
            Invoke("InsertStructure", 0, true, 2);
            Invoke("InsertStructure", 0, false, 3);
            Assert.That(((CellFormat)Invoke("GetDisplayFormat", 2, 3)).WrapText, Is.True);
            Assert.That((float)Invoke("ColumnWidth", 3), Is.EqualTo(190));
            Assert.That((float)Invoke("RowHeight", 2), Is.EqualTo(38));
            Undo.PerformUndo();
            Assert.That(((CellFormat)Invoke("GetDisplayFormat", 2, 0)).WrapText, Is.True);
            Undo.PerformUndo();
            Assert.That(((CellFormat)Invoke("GetDisplayFormat", 0, 0)).WrapText, Is.True);
            Assert.That(Document.GetText(0, 0), Is.EqualTo(LongHeading));
        }

        /// <summary>加号在选区下方或右侧插入并移动原数据，右键批量插入也以选区为参照。</summary>
        [UnityTest]
        public IEnumerator HeaderPlusButtonsInsertAfterSelection()
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
                int height = Document.RowCount;
                int width = Document.ColumnCount;
                ClickInsertButton(window, inspector, false);
                Assert.That(Document.GetText(0, 2), Is.EqualTo("other"));
                ClickInsertButton(window, inspector, false);
                ClickInsertButton(window, inspector, true);
                ClickInsertButton(window, inspector, true);
                Assert.That(Document.RowCount, Is.EqualTo(height + 2));
                Assert.That(Document.ColumnCount, Is.EqualTo(width + 2));
                Undo.PerformUndo();
                Assert.That(Document.RowCount, Is.EqualTo(height + 1));
                Undo.PerformRedo();
                Assert.That(Document.RowCount, Is.EqualTo(height + 2));

                Rect grid = GetField<Rect>("_gridRect");
                Vector2 plus = inspector.worldBound.position + new Vector2(grid.xMax - 12, grid.y + 12);
                window.SendEvent(new Event { type = EventType.ContextClick, button = 1, mousePosition = plus });
                Assert.That(GetField<bool>("_showInsertion"), Is.True);
                Assert.That(GetField<bool>("_insertRows"), Is.False);
                Assert.That(GetField<int>("_insertPosition"), Is.EqualTo(1));
            }
            finally
            {
                window.Close();
            }
        }

        private void ClickInsertButton(EditorWindow window, VisualElement inspector, bool rows)
        {
            Rect grid = GetField<Rect>("_gridRect");
            Vector2 center = inspector.worldBound.position + (rows
                ? new Vector2(grid.x + 20, grid.yMax - 12)
                : new Vector2(grid.xMax - 12, grid.y + 12));
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = center });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = center });
        }
    }
}
