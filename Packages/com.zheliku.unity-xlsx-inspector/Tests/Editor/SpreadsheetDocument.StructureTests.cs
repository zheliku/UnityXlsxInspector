using System;
using System.IO;
using ClosedXML.Excel;
using NUnit.Framework;

namespace XlsxInspector.Tests
{
    // 验证真正的文件结构和保存回读，特别是新增内容为空时，不能仅靠有值的单元格推算范围。
    public partial class SpreadsheetDocumentTests
    {
        /// <summary>内容边界忽略追加空白，并在清空末尾内容后自动缩回；中间空格保持原位置。</summary>
        /// <param name="excel">是否验证 Excel；为 false 时验证 CSV。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void ContentBoundsIgnoreAppendedBlanksAndShrinkAfterClear(bool excel)
        {
            string path = excel ? CreateWorkbook() : Path.Combine(_directory, "bounds.csv");
            if (!excel)
            {
                File.WriteAllText(path, "first");
            }
            using (var document = new SpreadsheetDocument(path))
            {
                document.SetText(3, 3, excel ? "=A1" : "last");
                document.InsertRows(4, 5);
                document.InsertColumns(4, 3);
                Assert.That(document.ContentRowCount, Is.EqualTo(4));
                Assert.That(document.ContentColumnCount, Is.EqualTo(4));
                Assert.That(document.GetText(2, 2), Is.Empty);
                document.SetText(3, 3, "");
                Assert.That(document.ContentRowCount, Is.EqualTo(1));
                Assert.That(document.ContentColumnCount, Is.EqualTo(excel ? 2 : 1));
                document.Save();
            }
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.ContentRowCount, Is.EqualTo(1));
            Assert.That(reopened.ContentColumnCount, Is.EqualTo(excel ? 2 : 1));
        }

        /// <summary>追加多行多列后，即使还没有输入数据，保存重开仍保留新增空白范围。</summary>
        /// <param name="excel">是否验证 Excel；为 false 时验证 CSV。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void EmptyAppendedRowsAndColumnsSurviveSaving(bool excel)
        {
            string path = excel ? CreateWorkbook() : Path.Combine(_directory, "append.csv");
            if (!excel)
            {
                File.WriteAllText(path, "a,b\n1,2\n");
            }
            int height;
            int width;
            string original;
            using (var document = new SpreadsheetDocument(path))
            {
                height = document.RowCount;
                width = document.ColumnCount;
                original = document.GetText(0, 0);
                document.InsertRows(height, 3);
                document.InsertColumns(width, 2);
                Assert.That(document.RowCount, Is.EqualTo(height + 3));
                Assert.That(document.ColumnCount, Is.EqualTo(width + 2));
                document.Save();
            }
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.RowCount, Is.EqualTo(height + 3));
            Assert.That(reopened.ColumnCount, Is.EqualTo(width + 2));
            Assert.That(reopened.GetText(0, 0), Is.EqualTo(original));
            Assert.That(reopened.GetText(height + 2, width + 1), Is.Empty);
        }

        /// <summary>单列 CSV 的末尾空行不能退化为一个文件结束换行。</summary>
        [Test]
        public void SingleColumnCsvRetainsAppendedEmptyRecords()
        {
            string path = Path.Combine(_directory, "single.csv");
            File.WriteAllText(path, "first");
            using (var document = new SpreadsheetDocument(path))
            {
                document.InsertRows(1, 3);
                document.Save();
            }
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.RowCount, Is.EqualTo(4));
            Assert.That(reopened.ColumnCount, Is.EqualTo(1));
            Assert.That(reopened.GetText(3, 0), Is.Empty);
        }

        /// <summary>空 CSV 同样可以批量增加行列，并保存为可再次读取的空白矩形。</summary>
        [Test]
        public void EmptyCsvSupportsBatchInsertion()
        {
            string path = Path.Combine(_directory, "empty.csv");
            File.WriteAllText(path, "");
            using (var document = new SpreadsheetDocument(path))
            {
                document.InsertRows(0, 2);
                document.InsertColumns(0, 3);
                document.Save();
            }
            using var reopened = new SpreadsheetDocument(path);
            Assert.That(reopened.RowCount, Is.EqualTo(3));
            Assert.That(reopened.ColumnCount, Is.EqualTo(4));
        }

        /// <summary>一次批量插入由 Excel 库整体移动内容、样式和公式引用，不反复执行单行插入。</summary>
        [Test]
        public void BatchInsertionMovesExcelFormulasAndStyles()
        {
            string path = CreateWorkbook();
            using (var document = new SpreadsheetDocument(path))
            {
                document.InsertRows(0, 3);
                document.InsertColumns(0, 2);
                Assert.That(document.GetText(3, 2), Is.EqualTo("10"));
                Assert.That(document.GetText(3, 3, true), Is.EqualTo("=C4+1"));
                document.Save();
            }
            using var workbook = new XLWorkbook(path);
            Assert.That(workbook.Worksheet(1).Cell("C4").Style.Font.Bold, Is.True);
            Assert.That(workbook.Worksheet(1).Cell("D4").FormulaA1, Is.EqualTo("C4+1"));
            Assert.That(workbook.Worksheet(2).Cell("A1").GetString(), Is.EqualTo("kept"));
        }

        /// <summary>无效数量或超出 Excel 边界时，原工作区和修改标记都保持不变。</summary>
        [Test]
        public void InvalidBatchInsertionDoesNotPartiallyModifyWorkbook()
        {
            using var document = new SpreadsheetDocument(CreateWorkbook());
            int height = document.RowCount;
            int width = document.ColumnCount;
            Assert.Throws<ArgumentOutOfRangeException>(() => document.InsertRows(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => document.InsertColumns(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => document.InsertRows(0, document.MaxRows));
            Assert.Throws<ArgumentOutOfRangeException>(() => document.InsertColumns(0, document.MaxColumns));
            Assert.That(document.RowCount, Is.EqualTo(height));
            Assert.That(document.ColumnCount, Is.EqualTo(width));
            Assert.That(document.IsDirty, Is.False);
        }
    }
}
