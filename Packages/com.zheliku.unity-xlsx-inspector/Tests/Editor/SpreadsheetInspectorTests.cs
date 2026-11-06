using System;
using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XlsxInspector.Tests
{
    /// <summary>通过真实 Unity Editor、Undo 和资源生命周期验证表格交互，不使用鼠标像素位置。</summary>
    public partial class SpreadsheetInspectorTests
    {
        private const string LongHeading = "A deliberately long heading that needs a wider spreadsheet column"; // 自适应宽度的测试内容。
        private string _folder; // 当前测试专属的 Assets 临时目录。
        private string _csvPath; // 临时 CSV 的 Unity 资源路径。
        private string _xlsxPath; // 临时 Excel 的 Unity 资源路径。
        private string[] _guids; // 用于清理本次测试产生的草稿和历史。
        private SpreadsheetInspector _editor; // 当前模拟选中的表格 Inspector。
        private string _clipboard; // 测试结束后恢复用户原来的剪贴板。

        /// <summary>创建两张真实资源，测试期间不改动项目中的用户表格。</summary>
        [SetUp]
        public void SetUp()
        {
            _clipboard = EditorGUIUtility.systemCopyBuffer;
            _folder = "Assets/__XlsxInspectorInteraction_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
            _csvPath = _folder + "/data.csv";
            _xlsxPath = _folder + "/data.xlsx";
            File.WriteAllText(_csvPath, LongHeading + ",other\r\n\"two\nlines\",value");
            using (var workbook = new XLWorkbook())
            {
                workbook.AddWorksheet("First").Cell("A1").Value = 10;
                workbook.AddWorksheet("Second").Cell("A1").Value = "second";
                workbook.SaveAs(_xlsxPath);
            }
            AssetDatabase.ImportAsset(_csvPath);
            AssetDatabase.ImportAsset(_xlsxPath);
            _guids = new[] { AssetDatabase.AssetPathToGUID(_csvPath), AssetDatabase.AssetPathToGUID(_xlsxPath) };
        }

        /// <summary>只清理本次测试的资源、Undo 对象和草稿，保留用户的全局 Undo 历史。</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_editor);
            foreach (var item in Resources.FindObjectsOfTypeAll<SpreadsheetHistoryState>())
            {
                string guid = item.AssetGuid;
                if (Array.IndexOf(_guids, guid) >= 0)
                {
                    Undo.ClearUndo(item);
                    Object.DestroyImmediate(item);
                }
            }
            foreach (string guid in _guids)
            {
                string draft = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library/XlsxInspector", guid + ".json");
                if (File.Exists(draft))
                {
                    File.Delete(draft);
                }
                string key = "XlsxInspector.Layout." + guid;
                SessionState.EraseString(key);
                SessionState.EraseString(key + ".source");
                SessionState.EraseInt(key + ".saved");
                SessionState.EraseInt(key + ".revision");
                SessionState.EraseInt(key + ".step");
                SessionState.EraseInt(key + ".sheet");
                string viewKey = "UnityXlsxInspector." + Hash128.Compute(Application.dataPath) + "." + guid;
                EditorPrefs.DeleteKey(viewKey + ".view");
                EditorPrefs.DeleteKey(viewKey + ".csv");
                EditorPrefs.DeleteKey(viewKey + ".height");
            }
            AssetDatabase.DeleteAsset(_folder);
            EditorGUIUtility.systemCopyBuffer = _clipboard;
        }

        /// <summary>验证单元格提交、撤销、重做，以及切换资源后继续撤销；原文件始终不被自动保存。</summary>
        [Test]
        public void CellUndoRedoSurvivesAssetSelectionChanges()
        {
            Open(_csvPath);
            SetField("_editBuffer", "edited");
            SetField("_editChanged", true);
            _editor.Copy();
            Assert.That(Document.GetText(0, 0), Is.EqualTo("edited"));
            Undo.PerformUndo();
            Assert.That(Document.GetText(0, 0), Is.EqualTo(LongHeading));
            Assert.That(Document.IsDirty, Is.False);
            Undo.PerformRedo();
            Assert.That(Document.GetText(0, 0), Is.EqualTo("edited"));
            Open(_xlsxPath);
            Open(_csvPath);
            Assert.That(Document.GetText(0, 0), Is.EqualTo("edited"));
            Undo.PerformUndo();
            Assert.That(Document.GetText(0, 0), Is.EqualTo(LongHeading));
            Undo.PerformRedo();
            Assert.That(Document.GetText(0, 0), Is.EqualTo("edited"));
            Assert.That(File.ReadAllText(_csvPath), Does.StartWith(LongHeading));
        }

        /// <summary>在网格最后的预留空格输入后，立即补出下一行列，继续按 Tab 或方向键时不会卡在边界。</summary>
        /// <param name="excel">是否验证 Excel；为 false 时验证 CSV。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void EditingLastVisibleCellExtendsGridBeforeNavigation(bool excel)
        {
            Open(excel ? _xlsxPath : _csvPath);
            int row = GetField<int>("_rows") - 1;
            int column = GetField<int>("_columns") - 1;
            Invoke("Select", row, column, false);
            SetField("_editBuffer", "tail");
            SetField("_editChanged", true);
            Invoke("MoveSelection", 1, 0, false);
            Assert.That(Document.GetText(row, column), Is.EqualTo("tail"));
            Assert.That(GetField<int>("_rows"), Is.GreaterThan(row + 1));
            Assert.That(GetField<int>("_columns"), Is.GreaterThan(column + 1));
            Assert.That(GetField<Vector2Int>("_selected").x, Is.EqualTo(column + 1));
            Undo.PerformUndo();
            Assert.That(Document.GetText(row, column), Is.Empty);
        }

        /// <summary>验证保存后仍可撤销和重做，后续保存使用最新磁盘版本，不误报外部修改冲突。</summary>
        [Test]
        public void UndoAfterSaveCanBeSavedAgain()
        {
            Open(_csvPath);
            SetField("_editBuffer", "saved edit");
            SetField("_editChanged", true);
            _editor.Copy();
            Invoke("Save");
            Assert.That(File.ReadAllText(_csvPath), Does.StartWith("saved edit,"));
            Assert.That(Document.IsDirty, Is.False);
            Undo.PerformUndo();
            Assert.That(Document.GetText(0, 0), Is.EqualTo(LongHeading));
            Assert.That(Document.IsDirty, Is.True);
            Invoke("Save");
            Assert.That(File.ReadAllText(_csvPath), Does.StartWith(LongHeading));
            Undo.PerformRedo();
            Assert.That(Document.GetText(0, 0), Is.EqualTo("saved edit"));
            Assert.That(Document.IsDirty, Is.True);
            Invoke("Save");
            Assert.That(File.ReadAllText(_csvPath), Does.StartWith("saved edit,"));
            Assert.That(GetField<string>("_error"), Is.Null.Or.Empty);
        }

        /// <summary>验证选区中的所有行列统一改尺寸，对齐修改和尺寸修改分别占一个 Undo 步骤。</summary>
        [Test]
        public void SelectionDimensionsAndFormattingUndoAsSingleOperations()
        {
            Open(_xlsxPath);
            float originalWidth = Document.GetColumnWidth(0);
            SelectTwoByTwo();
            Invoke("ResizeSelection", 210f, 46f);
            Assert.That(Document.GetColumnWidth(0), Is.EqualTo(210).Within(1));
            Assert.That(Document.GetColumnWidth(1), Is.EqualTo(210).Within(1));
            Assert.That(Document.GetRowHeight(0), Is.EqualTo(46).Within(1));
            Assert.That(Document.GetRowHeight(1), Is.EqualTo(46).Within(1));
            Undo.PerformUndo();
            Assert.That(Document.GetColumnWidth(0), Is.EqualTo(originalWidth).Within(1));
            Undo.PerformRedo();
            Invoke("ApplyFormat", HorizontalCellAlignment.Center, VerticalCellAlignment.Center, true);
            Assert.That(Document.GetCellFormat(0, 0).WrapText, Is.True);
            Assert.That(Document.GetCellFormat(1, 1).Horizontal, Is.EqualTo(HorizontalCellAlignment.Center));
            Undo.PerformUndo();
            Assert.That(Document.GetCellFormat(0, 0).WrapText, Is.False);
            Assert.That(Document.GetColumnWidth(1), Is.EqualTo(210).Within(1));
            Undo.PerformRedo();
            Assert.That(Document.GetCellFormat(0, 0).WrapText, Is.True);
        }

        /// <summary>验证自适应使用实际文字度量，并且 CSV 的显示修改不会标记数据需要保存。</summary>
        [Test]
        public void CsvAutoFitAndDisplayFormattingCanBeUndone()
        {
            Open(_csvPath);
            SelectTwoByTwo();
            Invoke("AutoFit", false);
            Assert.That((float)Invoke("ColumnWidth", 0), Is.GreaterThan(120));
            Assert.That((float)Invoke("RowHeight", 1), Is.GreaterThan(24));
            Assert.That(Document.IsDirty, Is.False);
            Undo.PerformUndo();
            Assert.That((float)Invoke("ColumnWidth", 0), Is.EqualTo(120));
            Assert.That((float)Invoke("RowHeight", 1), Is.EqualTo(24));
            Undo.PerformRedo();
            Invoke("ApplyFormat", HorizontalCellAlignment.Right, VerticalCellAlignment.Top, true);
            Assert.That(((CellFormat)Invoke("GetDisplayFormat", 1, 1)).WrapText, Is.True);
            Assert.That(Document.IsDirty, Is.False);
            Open(_xlsxPath);
            Open(_csvPath);
            Assert.That(((CellFormat)Invoke("GetDisplayFormat", 1, 1)).Horizontal, Is.EqualTo(HorizontalCellAlignment.Right));
            Assert.That(File.ReadAllText(_csvPath), Does.StartWith(LongHeading));
        }

        /// <summary>字号变大后，自适应使用新字号重新测量，而不是仍按 Unity 默认字号计算。</summary>
        [Test]
        public void GridFontSizeChangesAutoFitWithoutChangingContents()
        {
            var settings = SpreadsheetSettings.instance;
            string savedSettings = JsonUtility.ToJson(settings);
            try
            {
                Open(_csvPath);
                string original = File.ReadAllText(_csvPath);
                JsonUtility.FromJsonOverwrite("{\"_fontSize\":10}", settings);
                Invoke("AutoFit", false);
                float smallWidth = (float)Invoke("ColumnWidth", 0);
                JsonUtility.FromJsonOverwrite("{\"_fontSize\":24}", settings);
                Invoke("AutoFit", false);
                Assert.That((float)Invoke("ColumnWidth", 0), Is.GreaterThan(smallWidth));
                Assert.That(Document.IsDirty, Is.False);
                Assert.That(File.ReadAllText(_csvPath), Is.EqualTo(original));
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(savedSettings, settings);
            }
        }

        /// <summary>清除会话状态和隐藏 Undo 对象后重新打开，仍恢复 CSV 尺寸、格式、面板高度和滚动。</summary>
        [Test]
        public void CsvLayoutSurvivesWithoutSessionStateOrUndoObjects()
        {
            Open(_csvPath);
            Invoke("ResizeSelection", 190f, 45f);
            Invoke("ApplyFormat", HorizontalCellAlignment.Center, VerticalCellAlignment.Top, true);
            SetField("_gridHeight", 330f);
            SetField("_scroll", new Vector2(15, 20));
            string key = GetField<string>("_layoutKey");
            Invoke("ClearHistory");
            Object.DestroyImmediate(_editor);
            _editor = null;
            SessionState.EraseString(key);
            SessionState.EraseInt(key + ".sheet");
            Open(_csvPath);
            Assert.That((float)Invoke("ColumnWidth", 0), Is.EqualTo(190));
            Assert.That((float)Invoke("RowHeight", 0), Is.EqualTo(45));
            Assert.That(((CellFormat)Invoke("GetDisplayFormat", 0, 0)).WrapText, Is.True);
            Assert.That(GetField<float>("_gridHeight"), Is.EqualTo(330));
            Assert.That(GetField<Vector2>("_scroll"), Is.EqualTo(new Vector2(15, 20)));
            Assert.That(Document.IsDirty, Is.False);
        }

        /// <summary>每张 Sheet 独立记住滚动位置，关闭并清除会话后仍恢复最近查看的 Sheet。</summary>
        [Test]
        public void SheetViewsRememberTheirPositionsAndLastSheet()
        {
            Open(_xlsxPath);
            SetField("_scroll", new Vector2(20, 30));
            Invoke("SwitchSheet", 1);
            SetField("_scroll", new Vector2(40, 50));
            Invoke("SwitchSheet", 0);
            Assert.That(GetField<Vector2>("_scroll"), Is.EqualTo(new Vector2(20, 30)));
            Invoke("SwitchSheet", 1);
            Assert.That(GetField<Vector2>("_scroll"), Is.EqualTo(new Vector2(40, 50)));
            string key = GetField<string>("_layoutKey");
            Invoke("ClearHistory");
            Object.DestroyImmediate(_editor);
            _editor = null;
            SessionState.EraseInt(key + ".sheet");
            Open(_xlsxPath);
            Assert.That(Document.SheetIndex, Is.EqualTo(1));
            Assert.That(GetField<Vector2>("_scroll"), Is.EqualTo(new Vector2(40, 50)));
        }

        /// <summary>验证从另一张 Sheet 撤销时，会回到被修改的 Sheet 和单元格。</summary>
        [Test]
        public void UndoReturnsToTheEditedSheet()
        {
            Open(_xlsxPath);
            SetField("_editBuffer", "42");
            SetField("_editChanged", true);
            _editor.Copy();
            Invoke("SwitchSheet", 1);
            Assert.That(Document.SheetIndex, Is.EqualTo(1));
            Undo.PerformUndo();
            Assert.That(Document.SheetIndex, Is.Zero);
            Assert.That(Document.GetText(0, 0), Is.EqualTo("10"));
            Undo.PerformRedo();
            Assert.That(Document.GetText(0, 0), Is.EqualTo("42"));
            Invoke("SwitchSheet", 1);
            SetField("_editBuffer", "next");
            SetField("_editChanged", true);
            _editor.Copy();
            Undo.PerformUndo();
            Assert.That(Document.SheetIndex, Is.EqualTo(1));
            Assert.That(Document.GetText(0, 0), Is.EqualTo("second"));
            Undo.PerformUndo();
            Assert.That(Document.SheetIndex, Is.Zero);
            Assert.That(Document.GetText(0, 0), Is.EqualTo("10"));
            Undo.PerformRedo();
            Assert.That(Document.SheetIndex, Is.Zero);
            Assert.That(Document.GetText(0, 0), Is.EqualTo("42"));
            Undo.PerformRedo();
            Assert.That(Document.SheetIndex, Is.EqualTo(1));
            Assert.That(Document.GetText(0, 0), Is.EqualTo("next"));
        }

        // 反射只用于把测试输入交给真实 Inspector 的缓冲区和操作入口，避免依赖窗口大小或鼠标坐标。
        private SpreadsheetDocument Document => GetField<SpreadsheetDocument>("_document");

        private void Open(string path)
        {
            Object.DestroyImmediate(_editor);
            _editor = (SpreadsheetInspector)Editor.CreateEditor(AssetDatabase.LoadMainAssetAtPath(path));
            Assert.That(GetField<string>("_error"), Is.Null.Or.Empty);
        }

        private void SelectTwoByTwo()
        {
            SetField("_anchor", Vector2Int.zero);
            SetField("_selected", new Vector2Int(1, 1));
        }

        private T GetField<T>(string name)
        {
            return (T)typeof(SpreadsheetInspector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_editor);
        }

        private void SetField(string name, object value)
        {
            typeof(SpreadsheetInspector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_editor, value);
        }

        private object Invoke(string name, params object[] arguments)
        {
            return typeof(SpreadsheetInspector).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_editor, arguments);
        }
    }
}
