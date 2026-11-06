using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XlsxInspector
{
    /// <summary>
    /// 读取和生成带引号转义的分隔文本，供 CSV 文件和 TSV 剪贴板共用。
    /// </summary>
    internal static class Csv
    {
        // 逐字符解析。引号内的分隔符和换行属于单元格内容，双引号通过两个双引号转义。
        /// <summary>将分隔文本解析成行列表，不进行数字或公式类型转换。</summary>
        /// <param name="text">完整的 CSV 或 TSV 文本。</param>
        /// <param name="separator">列分隔符，例如逗号、制表符或竖线。</param>
        /// <returns>每行的文本单元格列表，保留显式空白列，不增加末尾换行产生的虚假行。</returns>
        /// <exception cref="FormatException">引号字段没有闭合。</exception>
        internal static List<List<string>> Parse(string text, char separator)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c != '"')
                    {
                        field.Append(c);
                    }
                    else if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else if (c == '"' && field.Length == 0)
                {
                    quoted = true;
                }
                else if (c == separator)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r' || c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            if (quoted)
            {
                throw new FormatException("Unclosed quoted field in delimited text.");
            }

            if (field.Length > 0 || row.Count > 0 || (text.Length > 0 && text[text.Length - 1] == '"'))
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }

        // 输出时只对包含特殊字符的字段添加引号，使 Excel/WPS 可以无损粘贴多行单元格。
        /// <summary>将文本单元格写成带必要引号转义的分隔文本。</summary>
        /// <param name="rows">要写出的行和列，按原始顺序输出。</param>
        /// <param name="separator">列分隔符。</param>
        /// <param name="newline">行分隔符，通常为 LF 或 CRLF。</param>
        /// <returns>序列化后的文本；是否补末尾换行由文件调用方决定。</returns>
        internal static string Write(IEnumerable<IEnumerable<string>> rows, char separator, string newline)
        {
            return string.Join(newline, rows.Select(row =>
            {
                var fields = row.ToList();
                // 单列的空行需要显式写成空引号字段，否则末尾的新增空行会被当作普通文件结束换行。
                if (fields.Count == 0 || (fields.Count == 1 && fields[0].Length == 0))
                {
                    return "\"\"";
                }
                return string.Join(separator.ToString(), fields.Select(value =>
                    value.IndexOfAny(new[] { separator, '"', '\r', '\n' }) >= 0
                        ? "\"" + value.Replace("\"", "\"\"") + "\"" : value));
            }));
        }

        // 取前 20 条逻辑记录比较列数的一致性，避免表头中的普通标点干扰分隔符识别。
        /// <summary>从少量逻辑记录推断列分隔符，并保留第一条记录的换行方式。</summary>
        /// <param name="text">待识别的完整文件文本。</param>
        /// <returns>列数最一致的候选分隔符及记录换行符；无法识别时分别使用逗号和 LF。</returns>
        internal static (char Separator, string Newline) DetectFormat(string text)
        {
            var candidates = new[] { ',', ';', '\t', '|' };
            var counts = new int[candidates.Length];
            var records = new List<int[]>();
            bool quoted = false;
            string newline = "\n";
            bool foundNewline = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    quoted = !quoted;
                }

                if (quoted)
                {
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    bool crlf = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n';
                    if (!foundNewline)
                    {
                        newline = crlf ? "\r\n" : c.ToString();
                        foundNewline = true;
                    }
                    records.Add(counts);
                    counts = new int[candidates.Length];
                    if (records.Count == 20)
                    {
                        break;
                    }
                    if (crlf)
                    {
                        i++;
                    }
                    continue;
                }

                int index = Array.IndexOf(candidates, c);
                if (index >= 0)
                {
                    counts[index]++;
                }
            }
            if (records.Count < 20)
            {
                records.Add(counts);
            }
            int best = 0;
            int matches = 0;
            int separators = 0;
            for (int candidate = 0; candidate < candidates.Length; candidate++)
            {
                var mode = records.Select(record => record[candidate]).Where(count => count > 0)
                    .GroupBy(count => count).OrderByDescending(group => group.Count()).ThenByDescending(group => group.Key).FirstOrDefault();
                if (mode != null && (mode.Count() > matches || (mode.Count() == matches && mode.Key > separators)))
                {
                    best = candidate;
                    matches = mode.Count();
                    separators = mode.Key;
                }
            }
            return (candidates[best], newline);
        }
    }
}
