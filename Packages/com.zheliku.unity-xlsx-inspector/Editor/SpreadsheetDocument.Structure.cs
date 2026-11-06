using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OpenXmlDocument = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument;

namespace XlsxInspector
{
    // 行列结构在同一层处理：Excel 复用库的批量移动，CSV 操作文本列表；空白范围也随文件保存。
    internal sealed partial class SpreadsheetDocument
    {
        /// <summary>实际内容的行边界；空表为 0，公式和包含空格的文本也属于内容。</summary>
        internal int ContentRowCount => IsCsv ? _csvRows.FindLastIndex(row => row.Any(value => value.Length > 0)) + 1
            : Sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;

        /// <summary>实际内容的列边界；追加的空白列和仅有格式的单元格不扩大亮色区域。</summary>
        internal int ContentColumnCount => IsCsv ? _csvRows.Select(row => row.FindLastIndex(value => value.Length > 0) + 1).DefaultIfEmpty(0).Max()
            : Sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;

        /// <summary>在指定位置前批量插入行，原位置及后面的行整体下移。</summary>
        /// <param name="index">从 0 开始的插入位置，也可以位于当前工作区末尾。</param>
        /// <param name="count">要插入的行数，必须大于 0。</param>
        /// <exception cref="ArgumentOutOfRangeException">位置、数量无效，或插入后超过 Excel 行数限制。</exception>
        internal void InsertRows(int index, int count = 1)
        {
            ValidateInsertion(index, count, true);
            int width = ColumnCount;
            int height = Math.Max(RowCount, index) + count;
            if (IsCsv)
            {
                if (_csvRows.Count == 0)
                {
                    _csvRows.Add(Enumerable.Repeat("", width).ToList());
                }
                while (_csvRows.Count < index)
                {
                    _csvRows.Add(Enumerable.Repeat("", width).ToList());
                }
                _csvRows.InsertRange(index, Enumerable.Range(0, count)
                    .Select(_ => Enumerable.Repeat("", width).ToList()));
            }
            else
            {
                Sheet.Row(index + 1).InsertRowsAbove(count);
                _sheetSizes[Sheet.Name] = (height, width);
            }
            IsDirty = true;
        }

        /// <summary>在指定位置前批量插入列，原位置及后面的列整体右移。</summary>
        /// <param name="index">从 0 开始的插入位置，也可以位于当前工作区末尾。</param>
        /// <param name="count">要插入的列数，必须大于 0。</param>
        /// <exception cref="ArgumentOutOfRangeException">位置、数量无效，或插入后超过 Excel 列数限制。</exception>
        internal void InsertColumns(int index, int count = 1)
        {
            ValidateInsertion(index, count, false);
            int width = Math.Max(ColumnCount, index) + count;
            int height = RowCount;
            if (IsCsv)
            {
                if (_csvRows.Count == 0)
                {
                    _csvRows.Add(new List<string> { "" });
                }
                foreach (var row in _csvRows)
                {
                    while (row.Count < index)
                    {
                        row.Add("");
                    }
                    row.InsertRange(index, Enumerable.Repeat("", count));
                }
            }
            else
            {
                Sheet.Column(index + 1).InsertColumnsBefore(count);
                _sheetSizes[Sheet.Name] = (height, width);
            }
            IsDirty = true;
        }

        /// <summary>删除指定行，后面的数据和格式整体向上移动。</summary>
        /// <param name="index">从 0 开始的行索引。</param>
        internal void DeleteRow(int index)
        {
            if (IsCsv)
            {
                if (index < _csvRows.Count)
                {
                    _csvRows.RemoveAt(index);
                }
            }
            else
            {
                int height = RowCount;
                int width = ColumnCount;
                Sheet.Row(index + 1).Delete();
                _sheetSizes[Sheet.Name] = (Math.Max(1, height - (index < height ? 1 : 0)), width);
            }
            IsDirty = true;
        }

        /// <summary>删除指定列，后面的数据和格式整体向左移动。</summary>
        /// <param name="index">从 0 开始的列索引。</param>
        internal void DeleteColumn(int index)
        {
            if (IsCsv)
            {
                foreach (var row in _csvRows)
                {
                    if (index < row.Count)
                    {
                        row.RemoveAt(index);
                    }
                }
            }
            else
            {
                int height = RowCount;
                int width = ColumnCount;
                Sheet.Column(index + 1).Delete();
                _sheetSizes[Sheet.Name] = (height, Math.Max(1, width - (index < width ? 1 : 0)));
            }
            IsDirty = true;
        }

        /// <summary>在开始修改前检查插入范围，避免数量无效时产生部分写入。</summary>
        /// <param name="index">从 0 开始的插入位置。</param>
        /// <param name="count">需要插入的行或列数量。</param>
        /// <param name="rows">为 true 检查行，否则检查列。</param>
        internal void ValidateInsertion(int index, int count, bool rows)
        {
            int limit = rows ? MaxRows : MaxColumns;
            int size = rows ? RowCount : ColumnCount;
            if (index < 0 || count <= 0 || (long)Math.Max(size, index) + count > limit)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "The insertion position or count exceeds the worksheet limits.");
            }
        }

        // Excel 的 dimension 是工作区边界；只按非空单元格推算会丢失追加的空白行列。
        private void ReadSheetSizes()
        {
            _workbookStream.Position = 0;
            using (var package = OpenXmlDocument.Open(_workbookStream, false))
            {
                foreach (var worksheet in _workbook.Worksheets)
                {
                    var sheet = package.WorkbookPart.Workbook.Sheets.Elements<Sheet>()
                        .First(item => item.Name.Value == worksheet.Name);
                    var part = (WorksheetPart)package.WorkbookPart.GetPartById(sheet.Id.Value);
                    string reference = part.Worksheet.SheetDimension?.Reference?.Value ?? "A1";
                    var last = worksheet.Cell(reference.Split(':').Last()).Address;
                    _sheetSizes[worksheet.Name] = (last.RowNumber, last.ColumnNumber);
                }
            }
            _workbookStream.Position = 0;
        }

        private void WriteSheetSizes()
        {
            _workbookStream.Position = 0;
            using (var package = OpenXmlDocument.Open(_workbookStream, true))
            {
                foreach (var worksheet in _workbook.Worksheets)
                {
                    var size = _sheetSizes[worksheet.Name];
                    int rows = Math.Max(size.Rows, worksheet.LastRowUsed()?.RowNumber() ?? 1);
                    int columns = Math.Max(size.Columns, worksheet.LastColumnUsed()?.ColumnNumber() ?? 1);
                    _sheetSizes[worksheet.Name] = (rows, columns);
                    var sheet = package.WorkbookPart.Workbook.Sheets.Elements<Sheet>()
                        .First(item => item.Name.Value == worksheet.Name);
                    var part = (WorksheetPart)package.WorkbookPart.GetPartById(sheet.Id.Value);
                    part.Worksheet.SheetDimension = new SheetDimension
                    {
                        Reference = "A1:" + worksheet.Cell(rows, columns).Address.ToStringRelative(false)
                    };
                    part.Worksheet.Save();
                }
            }
            _workbookStream.Position = 0;
        }
    }
}
