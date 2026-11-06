using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace XlsxInspector
{
    // Excel 显示格式集中在此处转换；界面不直接访问 ClosedXML 的工作表对象。
    internal sealed partial class SpreadsheetDocument
    {
        /// <summary>读取单元格的水平、垂直对齐和换行格式。</summary>
        /// <param name="row">从 0 开始的行索引。</param>
        /// <param name="column">从 0 开始的列索引。</param>
        /// <returns>用于 Inspector 绘制的基本格式。</returns>
        internal CellFormat GetCellFormat(int row, int column)
        {
            var alignment = Sheet.Cell(row + 1, column + 1).Style.Alignment;
            HorizontalCellAlignment horizontal;
            switch (alignment.Horizontal)
            {
                case XLAlignmentHorizontalValues.Left:
                    horizontal = HorizontalCellAlignment.Left;
                    break;
                case XLAlignmentHorizontalValues.Center:
                case XLAlignmentHorizontalValues.CenterContinuous:
                    horizontal = HorizontalCellAlignment.Center;
                    break;
                case XLAlignmentHorizontalValues.Right:
                    horizontal = HorizontalCellAlignment.Right;
                    break;
                default:
                    horizontal = HorizontalCellAlignment.General;
                    break;
            }

            return new CellFormat
            {
                Horizontal = horizontal,
                Vertical = alignment.Vertical == XLAlignmentVerticalValues.Top ? VerticalCellAlignment.Top
                    : alignment.Vertical == XLAlignmentVerticalValues.Center ? VerticalCellAlignment.Center
                    : VerticalCellAlignment.Bottom,
                WrapText = alignment.WrapText
            };
        }

        /// <summary>取得 General 格式的实际水平对齐方向，CSV 的内容始终按文本左对齐。</summary>
        /// <param name="row">从 0 开始的行索引。</param>
        /// <param name="column">从 0 开始的列索引。</param>
        /// <returns>数字靠右、布尔值和错误居中、其他内容靠左。</returns>
        internal HorizontalCellAlignment GetAutomaticAlignment(int row, int column)
        {
            if (IsCsv)
            {
                return HorizontalCellAlignment.Left;
            }

            var cell = Sheet.Cell(row + 1, column + 1);
            XLDataType type = cell.HasFormula ? cell.CachedValue.Type : cell.DataType;
            if (type == XLDataType.Number || type == XLDataType.DateTime || type == XLDataType.TimeSpan)
            {
                return HorizontalCellAlignment.Right;
            }

            return type == XLDataType.Boolean || type == XLDataType.Error
                ? HorizontalCellAlignment.Center : HorizontalCellAlignment.Left;
        }

        /// <summary>批量修改闭合矩形范围内指定的格式，未提供的格式保持原样。</summary>
        /// <param name="firstRow">第一行索引，从 0 开始。</param>
        /// <param name="firstColumn">第一列索引，从 0 开始。</param>
        /// <param name="lastRow">最后一行索引，包含该行。</param>
        /// <param name="lastColumn">最后一列索引，包含该列。</param>
        /// <param name="horizontal">要设置的水平对齐，null 表示不修改。</param>
        /// <param name="vertical">要设置的垂直对齐，null 表示不修改。</param>
        /// <param name="wrapText">是否自动换行，null 表示不修改。</param>
        internal void SetCellFormat(int firstRow, int firstColumn, int lastRow, int lastColumn,
            HorizontalCellAlignment? horizontal = null, VerticalCellAlignment? vertical = null,
            bool? wrapText = null)
        {
            var alignment = Sheet.Range(firstRow + 1, firstColumn + 1, lastRow + 1, lastColumn + 1).Style.Alignment;
            if (horizontal.HasValue)
            {
                switch (horizontal.Value)
                {
                    case HorizontalCellAlignment.Left:
                        alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        break;
                    case HorizontalCellAlignment.Center:
                        alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        break;
                    case HorizontalCellAlignment.Right:
                        alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        break;
                    default:
                        alignment.Horizontal = XLAlignmentHorizontalValues.General;
                        break;
                }
            }

            if (vertical.HasValue)
            {
                alignment.Vertical = vertical == VerticalCellAlignment.Top ? XLAlignmentVerticalValues.Top
                    : vertical == VerticalCellAlignment.Center ? XLAlignmentVerticalValues.Center
                    : XLAlignmentVerticalValues.Bottom;
            }

            if (wrapText.HasValue)
            {
                alignment.WrapText = wrapText.Value;
            }

            IsDirty = true;
        }


        // Excel 尺寸转换：列宽使用字符单位，行高使用磅；网格使用独立于 DPI 的像素单位。
        // 7 像素字符宽和 5 像素边距对应 Excel 常见的 Calibri 11 度量，不模拟全部字体排版。
        /// <summary>取得已定义行列的像素尺寸；不为灰色预览格逐个创建 ClosedXML 行列对象。</summary>
        /// <returns>默认行高、默认列宽，以及已定义的行高和列宽，字典索引从 0 开始。</returns>
        internal (float RowHeight, float ColumnWidth, Dictionary<int, float> Rows, Dictionary<int, float> Columns) ReadDimensions()
        {
            return ((float)(Sheet.RowHeight * 96 / 72), (float)(Sheet.ColumnWidth * 7 + 5),
                Sheet.Rows().ToDictionary(row => row.RowNumber() - 1, row => (float)(row.Height * 96 / 72)),
                Sheet.Columns().ToDictionary(column => column.ColumnNumber() - 1, column => (float)(column.Width * 7 + 5)));
        }

        /// <summary>读取 Excel 列宽，换算为网格显示使用的像素单位。</summary>
        /// <param name="column">从 0 开始的列索引；此方法仅用于 Excel。</param>
        /// <returns>列宽的像素近似值。</returns>
        internal float GetColumnWidth(int column)
        {
            return (float)(Sheet.Column(column + 1).Width * 7 + 5);
        }

        /// <summary>设置 Excel 列宽，并标记文档需要保存。</summary>
        /// <param name="column">从 0 开始的列索引。</param>
        /// <param name="pixels">界面已限制过的有效像素宽度。</param>
        internal void SetColumnWidth(int column, float pixels)
        {
            Sheet.Column(column + 1).Width = (pixels - 5) / 7;
            IsDirty = true;
        }

        /// <summary>读取 Excel 行高，将磅换算成 96 DPI 基准的像素单位。</summary>
        /// <param name="row">从 0 开始的行索引；此方法仅用于 Excel。</param>
        /// <returns>行高的像素近似值。</returns>
        internal float GetRowHeight(int row)
        {
            return (float)(Sheet.Row(row + 1).Height * 96 / 72);
        }

        /// <summary>设置 Excel 行高，并标记文档需要保存。</summary>
        /// <param name="row">从 0 开始的行索引。</param>
        /// <param name="pixels">界面已限制过的有效像素高度。</param>
        internal void SetRowHeight(int row, float pixels)
        {
            Sheet.Row(row + 1).Height = pixels * 72 / 96;
            IsDirty = true;
        }
    }
}
