using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector.Tests
{
    /// <summary>表格读写及 Unity Inspector 注册的 EditMode 回归测试，全部使用临时文件。</summary>
    public partial class SpreadsheetDocumentTests
    {
        private string _directory; // 每个测试独立的临时目录，避免修改真实项目表格。

        /// <summary>为当前测试创建唯一的临时目录。</summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "XlsxInspector-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        /// <summary>删除当前测试创建的临时文件。</summary>
        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        /// <summary>验证 CSV 保存保留分隔符、中文、引号、换行和以零开头的文本。</summary>
        [Test]
        public void CsvRoundTripPreservesQuotesMultilineUnicodeAndTextValues()
        {
            string path = Path.Combine(_directory, "data.csv");
            const string text = "id;name;note\r\n001;中文;\"a;\"\"b\"\"\r\nc\"\r\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
            using (var document = new SpreadsheetDocument(path))
            {
                Assert.That(document.GetText(1, 0), Is.EqualTo("001"));
                Assert.That(document.GetText(1, 2), Is.EqualTo("a;\"b\"\r\nc"));
                document.SetText(1, 1, "修改");
                document.Save();
            }
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes[0], Is.EqualTo((byte)'i'), "A BOM must not be added to a BOM-less file.");
            Assert.That(File.ReadAllText(path), Is.EqualTo(text.Replace("中文", "修改")));
        }

        /// <summary>验证 UTF-16 文件保存后仍保留 BOM 和原换行符。</summary>
        [Test]
        public void CsvRetainsUtf16BomAndNewlines()
        {
            string path = Path.Combine(_directory, "utf16.csv");
            File.WriteAllText(path, "a,b\n中文,c\n", Encoding.Unicode);
            using var document = new SpreadsheetDocument(path);
            document.SetText(1, 1, "新值");
            document.Save();
            Assert.That(File.ReadAllBytes(path)[0], Is.EqualTo(0xff));
            Assert.That(File.ReadAllText(path), Is.EqualTo("a,b\n中文,新值\n"));
        }

        /// <summary>竖线分隔文件可以正常分列；字段空格、引号内的分隔符和末尾空列在保存后保留。</summary>
        [Test]
        public void PipeDelimitedCsvPreservesPaddingQuotedValuesAndEmptyColumns()
        {
            string path = Path.Combine(_directory, "pipe.csv");
            const string text = "Id |Name       |Comment|Extra\r\nA0 |John       |\"a|b,;\t\r\nnext\"|\r\nB1 |James      |old|\r\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
            using (var document = new SpreadsheetDocument(path))
            {
                Assert.That(document.RowCount, Is.EqualTo(3));
                Assert.That(document.ColumnCount, Is.EqualTo(4));
                Assert.That(document.GetText(0, 0), Is.EqualTo("Id "));
                Assert.That(document.GetText(1, 1), Is.EqualTo("John       "));
                Assert.That(document.GetText(1, 2), Is.EqualTo("a|b,;\t\r\nnext"));
                Assert.That(document.GetText(2, 3), Is.Empty);
                document.SetText(2, 2, "new|value");
                document.Save();
            }
            Assert.That(File.ReadAllText(path), Is.EqualTo(text.Replace("|old|", "|\"new|value\"|")));
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.GetText(2, 2), Is.EqualTo("new|value"));
        }

        /// <summary>表头字段引号中的其他候选符号不能影响真实分隔符识别。</summary>
        /// <param name="separator">文件使用的分隔符。</param>
        [TestCase(',')]
        [TestCase(';')]
        [TestCase('\t')]
        [TestCase('|')]
        public void CsvDelimiterDetectionIgnoresQuotedCandidates(char separator)
        {
            string path = Path.Combine(_directory, "quoted-header.csv");
            string text = "\"name,;\t|\"" + separator + "value\nfirst" + separator + "42\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
            using var document = new SpreadsheetDocument(path);
            Assert.That(document.ColumnCount, Is.EqualTo(2));
            Assert.That(document.GetText(0, 0), Is.EqualTo("name,;\t|"));
            Assert.That(document.GetText(1, 1), Is.EqualTo("42"));
            document.SetText(1, 1, "43");
            document.Save();
            Assert.That(File.ReadAllText(path), Is.EqualTo(text.Replace("42", "43")));
        }

        /// <summary>表头中的逗号比实际分隔符多时，通过后续记录的一致列数识别分号。</summary>
        [Test]
        public void CsvDetectionUsesConsistentRecordsInsteadOfHeaderPunctuation()
        {
            string path = Path.Combine(_directory, "header-punctuation.csv");
            const string text = "id;description, note, status\n1;first\n2;second\n";
            File.WriteAllText(path, text);
            using var document = new SpreadsheetDocument(path);
            Assert.That(document.ColumnCount, Is.EqualTo(2));
            Assert.That(document.GetText(0, 1), Is.EqualTo("description, note, status"));
            document.SetText(2, 1, "changed");
            document.Save();
            Assert.That(File.ReadAllText(path), Is.EqualTo(text.Replace("second", "changed")));
        }

        /// <summary>默认命名空间和带前缀的 OOXML 等价；输出使用前者，兼容只识别裸标签的 Diff 读取器。</summary>
        [Test]
        public void WorkbookSaveUsesDefaultNamespacesForDiffReaders()
        {
            string path = CreateWorkbook();
            using (var document = new SpreadsheetDocument(path))
            {
                document.SetText(0, 0, "42");
                document.Save();
                document.GetBytes();
                document.Save();
            }
            using (var archive = ZipFile.OpenRead(path))
            {
                foreach (string part in new[] { "xl/workbook.xml", "xl/worksheets/sheet1.xml", "xl/styles.xml", "xl/sharedStrings.xml" })
                {
                    using var stream = archive.GetEntry(part).Open();
                    using var reader = new StreamReader(stream);
                    string xml = reader.ReadToEnd();
                    var root = XDocument.Parse(xml).Root;
                    Assert.That(root.GetDefaultNamespace().NamespaceName,
                        Is.EqualTo("http://schemas.openxmlformats.org/spreadsheetml/2006/main"));
                    Assert.That(xml, Does.Contain("<" + root.Name.LocalName));
                    if (part.Contains("worksheets"))
                    {
                        Assert.That(xml, Does.Contain("<sheetData>"));
                        Assert.That(xml, Does.Contain("<row "));
                    }
                }
            }
            using var workbook = new XLWorkbook(path);
            Assert.That(workbook.Worksheet(1).Cell("A1").GetDouble(), Is.EqualTo(42));
            Assert.That(workbook.Worksheet(1).Cell("B1").FormulaA1, Is.EqualTo("A1+1"));
            Assert.That(workbook.Worksheet(2).Cell("A1").GetString(), Is.EqualTo("kept"));
        }

        /// <summary>单元格内的换行不应改变 CSV 记录之间的换行格式。</summary>
        /// <param name="recordNewline">记录之间的原始换行符。</param>
        /// <param name="cellNewline">引号字段内部的换行符。</param>
        [TestCase("\n", "\r\n")]
        [TestCase("\r\n", "\n")]
        [TestCase("\r", "\r\n")]
        public void CsvKeepsRecordNewlinesWhenQuotedCellsUseDifferentNewlines(string recordNewline, string cellNewline)
        {
            string path = Path.Combine(_directory, "mixed-newlines.csv");
            string text = "\"first" + cellNewline + "cell\",value" + recordNewline + "last,old" + recordNewline;
            File.WriteAllText(path, text, new UTF8Encoding(false));
            using var document = new SpreadsheetDocument(path);
            document.SetText(1, 1, "new");
            document.Save();
            Assert.That(File.ReadAllText(path), Is.EqualTo(text.Replace("old", "new")));
        }

        /// <summary>验证剪贴板特殊字符和空白列，以及 CSV 行列插入删除后的内容。</summary>
        [Test]
        public void ClipboardRoundTripsQuotedTabsNewlinesAndEmptyTrailingCells()
        {
            string path = Path.Combine(_directory, "clipboard.csv");
            File.WriteAllText(path, "", new UTF8Encoding(false));
            using var document = new SpreadsheetDocument(path);
            const string clipboard = "one\t\"two\tthree\"\t\n\"four\nfive\"\t\"\"\"six\"\"\"\tend";
            document.Paste(0, 0, clipboard);
            Assert.That(document.GetText(0, 2), Is.Empty);
            Assert.That(document.Copy(0, 0, 1, 2), Is.EqualTo(clipboard));
            document.InsertRows(1);
            document.DeleteRow(1);
            document.InsertColumns(1);
            document.DeleteColumn(1);
            document.Save();
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.GetText(1, 0), Is.EqualTo("four\nfive"));
            Assert.That(reopened.GetText(1, 1), Is.EqualTo("\"six\""));
        }

        /// <summary>验证 Excel 类型推断、公式、格式和其他 Sheet 在保存后保留。</summary>
        [Test]
        public void XlsxEditingPreservesOtherSheetsFormulasAndStyles()
        {
            string path = CreateWorkbook();
            using (var document = new SpreadsheetDocument(path))
            {
                Assert.That(document.SheetNames, Is.EqualTo(new[] { "Config", "Other" }));
                document.SetText(0, 0, "15");
                document.SetText(1, 0, "001");
                document.SetText(2, 0, "=A1*2");
                document.SetText(3, 0, "'123");
                document.SetText(4, 0, "true");
                document.SheetIndex = 1;
                Assert.That(document.GetText(0, 0), Is.EqualTo("kept"));
                document.Save();
            }
            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("Config");
            Assert.That(sheet.Cell("A1").GetDouble(), Is.EqualTo(15));
            Assert.That(sheet.Cell("A1").Style.Font.Bold, Is.True);
            Assert.That(sheet.Cell("A2").DataType, Is.EqualTo(XLDataType.Text));
            Assert.That(sheet.Cell("A2").GetString(), Is.EqualTo("001"));
            Assert.That(sheet.Cell("A3").FormulaA1, Is.EqualTo("A1*2"));
            Assert.That(sheet.Cell("B1").FormulaA1, Is.EqualTo("A1+1"));
            Assert.That(sheet.Cell("A4").GetString(), Is.EqualTo("123"));
            Assert.That(sheet.Cell("A5").GetBoolean(), Is.True);
            Assert.That(workbook.Worksheet("Other").Cell("A1").GetString(), Is.EqualTo("kept"));
        }

        /// <summary>验证行列移动时 Excel 库同步修正公式引用。</summary>
        [Test]
        public void XlsxStructuralEditsMoveValuesAndFormulaReferences()
        {
            string path = CreateWorkbook();
            using (var document = new SpreadsheetDocument(path))
            {
                document.InsertRows(0);
                Assert.That(document.GetText(1, 1, true), Is.EqualTo("=A2+1"));
                document.InsertColumns(0);
                Assert.That(document.GetText(1, 2, true), Is.EqualTo("=B2+1"));
                document.DeleteRow(0);
                document.DeleteColumn(0);
                document.Save();
            }
            using var workbook = new XLWorkbook(path);
            Assert.That(workbook.Worksheet(1).Cell("A1").GetDouble(), Is.EqualTo(10));
            Assert.That(workbook.Worksheet(1).Cell("B1").FormulaA1, Is.EqualTo("A1+1"));
        }

        /// <summary>验证草稿恢复保留未保存编辑，同时不修改原文件。</summary>
        [Test]
        public void DraftRetainsUnsavedWorkbookAndOriginalSourceVersion()
        {
            string path = CreateWorkbook();
            using var document = new SpreadsheetDocument(path);
            document.SheetIndex = 1;
            document.SetText(0, 0, "draft");
            using var restored = new SpreadsheetDocument(path, document.GetBytes(), document.SourceHash);
            restored.SheetIndex = 1;
            Assert.That(restored.IsDirty, Is.True);
            Assert.That(restored.GetText(0, 0), Is.EqualTo("draft"));
            using var original = new XLWorkbook(path);
            Assert.That(original.Worksheet(2).Cell("A1").GetString(), Is.EqualTo("kept"));
        }

        /// <summary>验证网格像素尺寸能写回 Excel，并在草稿和再次加载后保留。</summary>
        [Test]
        public void ExcelColumnWidthAndRowHeightRoundTrip()
        {
            string path = CreateWorkbook();
            using (var document = new SpreadsheetDocument(path))
            {
                document.SetColumnWidth(1, 240);
                document.SetRowHeight(0, 48);
                Assert.That(document.IsDirty, Is.True);
                using var draft = new SpreadsheetDocument(path, document.GetBytes(), document.SourceHash);
                Assert.That(draft.GetColumnWidth(1), Is.EqualTo(240).Within(1));
                Assert.That(draft.GetRowHeight(0), Is.EqualTo(48).Within(1));
                draft.Save();
            }
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.GetColumnWidth(1), Is.EqualTo(240).Within(1));
            Assert.That(reopened.GetRowHeight(0), Is.EqualTo(48).Within(1));
        }

        /// <summary>验证范围对齐和换行能保存回 Excel，内容与未选中的格式保持不变。</summary>
        [Test]
        public void CellFormattingRoundTripsWithoutChangingOtherCells()
        {
            string path = CreateWorkbook();
            using (var document = new SpreadsheetDocument(path))
            {
                document.SetCellFormat(0, 0, 1, 1, HorizontalCellAlignment.Center,
                    VerticalCellAlignment.Center, wrapText: true);
                document.Save();
            }
            using var reopened = new SpreadsheetDocument(path);
            CellFormat format = reopened.GetCellFormat(1, 1);
            Assert.That(format.Horizontal, Is.EqualTo(HorizontalCellAlignment.Center));
            Assert.That(format.Vertical, Is.EqualTo(VerticalCellAlignment.Center));
            Assert.That(format.WrapText, Is.True);
            Assert.That(reopened.GetText(0, 0), Is.EqualTo("10"));
            Assert.That(reopened.GetCellFormat(2, 2).WrapText, Is.False);
            reopened.SheetIndex = 1;
            Assert.That(reopened.GetText(0, 0), Is.EqualTo("kept"));
        }

        /// <summary>验证检测到外部修改后拒绝覆盖，且继续保留本地编辑。</summary>
        [Test]
        public void SaveRejectsExternalChangesWithoutOverwritingThem()
        {
            string path = Path.Combine(_directory, "conflict.csv");
            File.WriteAllText(path, "original");
            using var document = new SpreadsheetDocument(path);
            document.SetText(0, 0, "local");
            File.WriteAllText(path, "external");
            Assert.Throws<IOException>(() => document.Save());
            Assert.That(File.ReadAllText(path), Is.EqualTo("external"));
            Assert.That(document.IsDirty, Is.True);
        }

        /// <summary>验证生成草稿后仍能反复保存，覆盖数据流生命周期的回归问题。</summary>
        [Test]
        public void WorkbookCanSaveRepeatedlyAfterDraftSnapshots()
        {
            string path = CreateWorkbook();
            using var document = new SpreadsheetDocument(path);
            document.SetText(0, 0, "20");
            document.GetBytes();
            document.Save();
            document.SetText(0, 0, "30");
            document.GetBytes();
            document.Save();
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.GetText(0, 0), Is.EqualTo("30"));
        }

        /// <summary>验证缺少默认样式标识的工作簿，以及旧版本生成的越界草稿能够恢复和保存。</summary>
        /// <param name="hasBrokenDuplicate">是否模拟旧草稿中重复且越界的 Normal 样式。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void WorkbookWithoutBuiltinNormalStyleCanRestoreDraftAndSave(bool hasBrokenDuplicate)
        {
            string path = Path.Combine(_directory, "normal-style.xlsx");
            using (var workbook = new XLWorkbook())
            {
                workbook.AddWorksheet("Config").Cell("A1").Value = "original";
                workbook.SaveAs(path);
            }
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                var entry = archive.GetEntry("xl/styles.xml");
                XDocument xml;
                using (var stream = entry.Open())
                {
                    xml = XDocument.Load(stream);
                }

                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var style in xml.Descendants(ns + "cellStyle"))
                {
                    style.Attribute("builtinId")?.Remove();
                }

                if (hasBrokenDuplicate)
                {
                    var styles = xml.Root.Element(ns + "cellStyles");
                    styles.Add(new XElement(ns + "cellStyle", new XAttribute("name", "Normal"),
                        new XAttribute("xfId", xml.Root.Element(ns + "cellXfs").Elements().Count()), new XAttribute("builtinId", 0)));
                    styles.SetAttributeValue("count", styles.Elements().Count());
                }
                entry.Delete();
                using var output = archive.CreateEntry("xl/styles.xml").Open();
                xml.Save(output);
            }
            using (var document = new SpreadsheetDocument(path))
            {
                Assert.That(document.Copy(0, 0, 0, 0), Is.EqualTo("original"));
                Assert.That(document.IsDirty, Is.False, "Copy must not create a draft.");
                document.SetText(0, 0, "edited");
                using var restored = new SpreadsheetDocument(path, document.GetBytes(), document.SourceHash);
                Assert.That(restored.GetText(0, 0), Is.EqualTo("edited"));
                restored.Save();
            }
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.GetText(0, 0), Is.EqualTo("edited"));
        }

        /// <summary>通过 Unity 实际导入验证原生图标类型、Inspector 注册和复制切换返回流程。</summary>
        [Test]
        public void NativeXlsxAndCsvAssetsUseSpreadsheetInspectors()
        {
            string folder = "Assets/__XlsxInspectorTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string clipboard = EditorGUIUtility.systemCopyBuffer;
            try
            {
                string xlsx = folder + "/config.xlsx", csv = folder + "/config.csv";
                File.Copy(CreateWorkbook(), xlsx);
                File.WriteAllText(csv, "a,b\n1,2");
                AssetDatabase.ImportAsset(xlsx);
                AssetDatabase.ImportAsset(csv);
                var xlsxAsset = AssetDatabase.LoadMainAssetAtPath(xlsx);
                var csvAsset = AssetDatabase.LoadMainAssetAtPath(csv);
                Assert.That(xlsxAsset, Is.TypeOf<DefaultAsset>(), "Keep Unity's original xlsx importer and icon.");
                Assert.That(csvAsset, Is.TypeOf<TextAsset>());
                var xlsxEditor = Editor.CreateEditor(xlsxAsset);
                var csvEditor = Editor.CreateEditor(csvAsset);
                try
                {
                    Assert.That(xlsxEditor, Is.TypeOf<SpreadsheetInspector>());
                    Assert.That(csvEditor, Is.TypeOf<CsvInspector>());
                    ((SpreadsheetInspector)xlsxEditor).Copy();
                    string copied = EditorGUIUtility.systemCopyBuffer;
                    Assert.That(copied, Is.EqualTo("10"), "Copy must read the actual active cell.");
                    UnityEngine.Object.DestroyImmediate(xlsxEditor);
                    UnityEngine.Object.DestroyImmediate(csvEditor);
                    csvEditor = null;
                    xlsxEditor = Editor.CreateEditor(xlsxAsset);
                    ((SpreadsheetInspector)xlsxEditor).Copy();
                    Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(copied), "Copy, switch to CSV and return must retain a readable workbook.");
                }
                finally { UnityEngine.Object.DestroyImmediate(xlsxEditor); UnityEngine.Object.DestroyImmediate(csvEditor); }
            }
            finally { EditorGUIUtility.systemCopyBuffer = clipboard; AssetDatabase.DeleteAsset(folder); }
        }

        /// <summary>验证格式错误的剪贴板文本在解析阶段报告错误。</summary>
        [Test]
        public void ClipboardRejectsUnclosedQuotes()
        {
            Assert.Throws<FormatException>(() => Csv.Parse("\"unclosed", '\t'));
        }

        /// <summary>验证非表格 TextAsset 仍然可以创建 Unity 原生文本预览。</summary>
        [Test]
        public void InspectorsKeepDefaultAssetsAndNonSpreadsheetInspectors()
        {
            var text = new TextAsset("standard text preview");
            var inspector = Editor.CreateEditor(text);
            try
            {
                Assert.That(inspector, Is.TypeOf<CsvInspector>());
                Assert.That(typeof(Editor).Assembly.GetType("UnityEditor.DefaultAssetInspector"), Is.Not.Null);
                Assert.That(typeof(Editor).Assembly.GetType("UnityEditor.TextAssetInspector"), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(inspector); UnityEngine.Object.DestroyImmediate(text); }
        }

        string CreateWorkbook()
        {
            string path = Path.Combine(_directory, "config.xlsx");
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("Config");
            sheet.Cell("A1").Value = 10;
            sheet.Cell("A1").Style.Font.Bold = true;
            sheet.Cell("B1").FormulaA1 = "A1+1";
            workbook.AddWorksheet("Other").Cell("A1").Value = "kept";
            workbook.SaveAs(path);
            return path;
        }
    }
}
