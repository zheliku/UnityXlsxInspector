using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;

namespace XlsxInspector
{
    /// <summary>
    /// 管理一个 Excel 或 CSV 文档的数据。界面只调用此类操作文件内容，不直接处理 Excel XML。
    /// </summary>
    /// <remarks>
    /// 单元格、行和列索引均从 0 开始；ClosedXML 的索引从 1 开始，转换集中在此类内部。
    /// Excel 的源数据流与工作簿共用生命周期，关闭 Inspector 时通过 Dispose 释放。
    /// </remarks>
    internal sealed partial class SpreadsheetDocument : IDisposable
    {
        private readonly string _filePath; // 原文件的绝对路径；只有显式保存时才写入。
        private readonly XLWorkbook _workbook; // Excel 工作簿；CSV 模式下为空。
        private readonly MemoryStream _workbookStream; // ClosedXML 会继续使用源流，必须保留到文档关闭。
        private readonly List<List<string>> _csvRows; // CSV 的原始文本值；不进行数字或公式推断。
        private readonly Encoding _encoding; // CSV 原始编码及 BOM 设置。
        private readonly char _separator; // CSV 使用的逗号、分号、制表符或竖线。
        private readonly string _newline; // CSV 原始换行符。
        private readonly bool _hasTrailingNewline; // 是否保留 CSV 文件末尾的换行符。
        private readonly Dictionary<string, (int Rows, int Columns)> _sheetSizes = new Dictionary<string, (int, int)>(); // Excel 工作区范围，包含用户追加的空白行列。

        /// <summary>加载时原文件的 SHA-256，用来检测外部修改；草稿恢复时继续使用原来的版本。</summary>
        internal string SourceHash
        {
            get;
            private set;
        }

        /// <summary>当前是否有尚未保存的内容或 Excel 尺寸修改。复制和读取不会设置此标记。</summary>
        internal bool IsDirty
        {
            get;
            private set;
        }

        /// <summary>当前工作表索引，从 0 开始；切换工作表不会释放或重新加载整个文档。</summary>
        internal int SheetIndex
        {
            get;
            set;
        }

        /// <summary>工作表名称，顺序与 Excel 文件中的顺序一致；CSV 只有一个工作表。</summary>
        internal string[] SheetNames
        {
            get;
        }

        /// <summary>文档是否是 CSV；CSV 只包含文本数据，不支持工作表格式。</summary>
        internal bool IsCsv => _workbook == null;

        /// <summary>当前文件格式允许的最大行数。</summary>
        internal int MaxRows => IsCsv ? int.MaxValue : XLHelper.MaxRowNumber;

        /// <summary>当前文件格式允许的最大列数。</summary>
        internal int MaxColumns => IsCsv ? int.MaxValue : XLHelper.MaxColumnNumber;

        // 当前 Excel 工作表只在 Excel 路径中访问，避免让界面依赖 ClosedXML 类型。
        private IXLWorksheet Sheet => _workbook.Worksheet(SheetIndex + 1);

        /// <summary>当前工作区行数，包含追加的空白行；空表返回 1，保证始终有可编辑的 A1。</summary>
        internal int RowCount => IsCsv ? Math.Max(1, _csvRows.Count)
            : Math.Max(Sheet.LastRowUsed()?.RowNumber() ?? 1, _sheetSizes[Sheet.Name].Rows);

        /// <summary>当前工作区列数，包含追加的空白列；空表返回 1。</summary>
        internal int ColumnCount
        {
            get
            {
                if (IsCsv)
                {
                    return Math.Max(1, _csvRows.Count == 0 ? 0 : _csvRows.Max(row => row.Count));
                }

                return Math.Max(Sheet.LastColumnUsed()?.ColumnNumber() ?? 1, _sheetSizes[Sheet.Name].Columns);
            }
        }

        // 文档加载：读取原件或恢复草稿，原件的内容在此阶段始终保持不变。
        /// <summary>打开原文件，或用草稿数据恢复尚未保存的编辑。</summary>
        /// <param name="path">原文件的绝对路径，扩展名应为 .xlsx 或 .csv。</param>
        /// <param name="draft">可选的草稿文件字节；为空时读取原文件。</param>
        /// <param name="sourceHash">草稿对应的原文件版本；恢复草稿时用于检测外部修改。</param>
        /// <param name="isDirty">恢复撤销快照时，按它与已保存版本的关系恢复修改标记。</param>
        internal SpreadsheetDocument(string path, byte[] draft = null, string sourceHash = null, bool isDirty = true)
        {
            _filePath = path;
            byte[] source = File.ReadAllBytes(path);
            SourceHash = sourceHash ?? Hash(source);
            byte[] data = draft ?? source;
            IsDirty = draft != null && isDirty;
            if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                // StreamReader 会识别 UTF-8/16/32 的 BOM；没有 BOM 时严格按 UTF-8 读取，避免默默损坏其他编码。
                using var stream = new MemoryStream(data);
                using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
                string text = reader.ReadToEnd();
                _encoding = reader.CurrentEncoding;
                var format = Csv.DetectFormat(text);
                _separator = format.Separator;
                _newline = format.Newline;
                _hasTrailingNewline = text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith("\r", StringComparison.Ordinal);
                _csvRows = Csv.Parse(text, _separator);
                SheetNames = new[] { Path.GetFileNameWithoutExtension(path) };
            }
            else
            {
                _workbookStream = new MemoryStream();
                _workbookStream.Write(data, 0, data.Length);
                _workbookStream.Position = 0;
                NormalizeDefaultStyle(_workbookStream);
                _workbook = new XLWorkbook(_workbookStream);
                SheetNames = _workbook.Worksheets.Select(sheet => sheet.Name).ToArray();
                ReadSheetSizes();
            }
        }

        // 单元格读写：显示文本、编辑文本与文件中的真实类型在这里转换。
        /// <summary>读取单元格用于显示或编辑的文本，不改变文档。</summary>
        /// <param name="row">从 0 开始的行索引。</param>
        /// <param name="column">从 0 开始的列索引。</param>
        /// <param name="formula">为 true 时读取公式原文；为 false 时优先显示缓存计算结果。</param>
        /// <returns>单元格文本；不存在的单元格返回空字符串。</returns>
        internal string GetText(int row, int column, bool formula = false)
        {
            if (IsCsv)
            {
                return row < _csvRows.Count && column < _csvRows[row].Count ? _csvRows[row][column] : "";
            }

            var cell = Sheet.Cell(row + 1, column + 1);
            if (cell.HasFormula)
            {
                return formula || cell.CachedValue.IsBlank ? "=" + cell.FormulaA1 : cell.CachedValue.ToString(CultureInfo.InvariantCulture);
            }

            return cell.GetFormattedString(CultureInfo.InvariantCulture);
        }

        /// <summary>修改单元格内容；Excel 推断公式、布尔值和数字，CSV 始终保留文本。</summary>
        /// <param name="row">从 0 开始的行索引。</param>
        /// <param name="column">从 0 开始的列索引。</param>
        /// <param name="text">编辑文本。Excel 中以单引号开头可强制文本，以等号开头表示公式。</param>
        /// <exception cref="ArgumentOutOfRangeException">单元格超出文件格式允许的范围。</exception>
        internal void SetText(int row, int column, string text)
        {
            if (row < 0 || row >= MaxRows || column < 0 || column >= MaxColumns)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "The selection exceeds the worksheet limits.");
            }

            if (GetText(row, column, true) == text)
            {
                return;
            }

            if (IsCsv)
            {
                while (_csvRows.Count <= row)
                {
                    _csvRows.Add(new List<string>());
                }

                while (_csvRows[row].Count <= column)
                {
                    _csvRows[row].Add("");
                }

                _csvRows[row][column] = text;
            }
            else
            {
                var cell = Sheet.Cell(row + 1, column + 1);
                cell.Clear(XLClearOptions.Contents);
                if (text.StartsWith("=", StringComparison.Ordinal) && text.Length > 1)
                {
                    cell.FormulaA1 = text.Substring(1);
                }
                else if (text.StartsWith("'", StringComparison.Ordinal))
                {
                    cell.Value = text.Substring(1);
                }
                else if (bool.TryParse(text, out bool boolean))
                {
                    cell.Value = boolean;
                }
                else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                         && !double.IsNaN(number) && !double.IsInfinity(number)
                         && !(text.Length > 1 && text[0] == '0' && char.IsDigit(text[1])))
                {
                    cell.Value = number;
                }
                else
                {
                    cell.Value = text;
                }
            }
            IsDirty = true;
        }


        // 剪贴板使用 TSV；共享 CSV 的引号规则，保留单元格内的制表符和换行。
        /// <summary>从指定单元格开始粘贴制表符分隔的矩形文本。</summary>
        /// <param name="row">粘贴起点的行索引，从 0 开始。</param>
        /// <param name="column">粘贴起点的列索引，从 0 开始。</param>
        /// <param name="text">从 Excel、WPS 或本 Inspector 复制的 TSV 文本。</param>
        /// <exception cref="ArgumentException">粘贴区域超出工作表范围。</exception>
        internal void Paste(int row, int column, string text)
        {
            var values = Csv.Parse(text, '\t');
            if (values.Count == 0)
            {
                values.Add(new List<string> { "" });
            }

            if ((long)row + values.Count > MaxRows || (long)column + values.Max(value => value.Count) > MaxColumns)
            {
                throw new ArgumentException("The pasted range exceeds the worksheet limits.");
            }

            for (int r = 0; r < values.Count; r++)
            {
                for (int c = 0; c < values[r].Count; c++)
                {
                    SetText(row + r, column + c, values[r][c]);
                }
            }
        }

        /// <summary>将选中的闭合矩形区域转换为可交换的 TSV 文本。</summary>
        /// <param name="firstRow">第一行索引，从 0 开始。</param>
        /// <param name="firstColumn">第一列索引，从 0 开始。</param>
        /// <param name="lastRow">最后一行索引，包含该行。</param>
        /// <param name="lastColumn">最后一列索引，包含该列。</param>
        /// <returns>包含必要引号转义的 TSV；公式保留原文。</returns>
        internal string Copy(int firstRow, int firstColumn, int lastRow, int lastColumn)
        {
            return Csv.Write(Enumerable.Range(firstRow, lastRow - firstRow + 1).Select(row =>
                Enumerable.Range(firstColumn, lastColumn - firstColumn + 1).Select(column => GetText(row, column, true))), '\t', "\n");
        }

        // 草稿与显式保存共用序列化路径，保证它们能被相同的加载逻辑重新打开。
        /// <summary>把当前内存文档序列化为文件数据，不写入原文件，不清除修改标记。</summary>
        /// <returns>可直接重新打开的 .xlsx 或 .csv 文件字节。</returns>
        internal byte[] GetBytes()
        {
            if (!IsCsv)
            {
                // ClosedXML retains its source stream and reuses it for subsequent saves.
                _workbook.SaveAs(_workbookStream, new SaveOptions { EvaluateFormulasBeforeSaving = false, GenerateCalculationChain = false });
                WriteSheetSizes();
                NormalizeSpreadsheetNamespaces();
                return _workbookStream.ToArray();
            }
            using var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, _encoding, 1024, true))
            {
                writer.Write(Csv.Write(_csvRows, _separator, _newline));
                if (_hasTrailingNewline)
                {
                    writer.Write(_newline);
                }
            }
            return stream.ToArray();
        }

        /// <summary>检查外部修改后，将内存内容通过同目录临时文件替换原文件。</summary>
        /// <exception cref="IOException">原文件被外部修改、锁定，或文件写入失败。</exception>
        internal void Save()
        {
            if (Hash(File.ReadAllBytes(_filePath)) != SourceHash)
            {
                throw new IOException("The source file changed on disk. Copy your edits, then use Reload to load the external changes before saving.");
            }

            byte[] data = GetBytes();
            string temporary = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, data);
                File.Replace(temporary, _filePath, null);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            SourceHash = Hash(data);
            IsDirty = false;
        }

        // 文件哈希比较实际内容，避免依赖容易变化或精度不足的文件时间戳。
        private static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(bytes));
        }

        // 兼容已经复现的默认样式问题，同时恢复旧版本生成的草稿，不丢弃用户编辑。
        private static void NormalizeDefaultStyle(Stream stream)
        {
            // Some valid producers omit builtinId on Normal. ClosedXML then adds a
            // second Normal with an invalid format index, making its own output unreadable.
            // Mark the existing style before loading; also recover drafts already written that way.
            using (var package = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, true))
            {
                var styles = package.WorkbookPart.WorkbookStylesPart?.Stylesheet;
                var normal = styles?.CellStyles?.Elements<DocumentFormat.OpenXml.Spreadsheet.CellStyle>()
                    .FirstOrDefault(style => style.Name?.Value == "Normal" && style.FormatId?.Value == 0);
                if (normal != null && normal.BuiltinId == null)
                {
                    normal.BuiltinId = 0;
                    var duplicates = styles.CellStyles.Elements<DocumentFormat.OpenXml.Spreadsheet.CellStyle>()
                        .Where(style => style != normal && style.Name?.Value == "Normal" && style.BuiltinId?.Value == 0
                            && style.FormatId?.Value >= (styles.CellFormats?.ChildElements.Count ?? 0)).ToList();
                    foreach (var duplicate in duplicates)
                    {
                        duplicate.Remove();
                    }

                    styles.CellStyles.Count = (uint)styles.CellStyles.ChildElements.Count;
                    styles.Save();
                }
            }
            stream.Position = 0;
        }

        /// <summary>关闭工作簿和它保留的源数据流；关闭后不能继续读写此文档。</summary>
        public void Dispose()
        {
            _workbook?.Dispose();
            _workbookStream?.Dispose();
        }
    }
}
