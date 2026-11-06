using System;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    // 此文件负责显示格式和自适应尺寸，Excel 只修改对应格式项，CSV 保存在会话布局中。
    public partial class SpreadsheetInspector
    {
        private CellFormat GetDisplayFormat(int row, int column)
        {
            if (!_document.IsCsv)
            {
                return _document.GetCellFormat(row, column);
            }

            var entry = _csvLayout.cellFormats.Find(item => item.row == row && item.column == column);
            return entry == null ? CellFormat.Default : entry.format;
        }

        private TextAnchor GetTextAnchor(int row, int column, CellFormat format)
        {
            HorizontalCellAlignment horizontal = format.Horizontal == HorizontalCellAlignment.General
                ? _document.GetAutomaticAlignment(row, column) : format.Horizontal;
            // Unity 的 TextAnchor 按上/中/下三行排列，每行再按左/中/右排列。
            return (TextAnchor)((int)format.Vertical * 3 + (int)horizontal - 1);
        }

        // 菜单中的格式操作面向整个选区，不只修改当前活动单元格。
        private void AddFormatMenuItems(GenericMenu menu)
        {
            CellFormat current = GetDisplayFormat(_selected.y, _selected.x);
            foreach (HorizontalCellAlignment value in Enum.GetValues(typeof(HorizontalCellAlignment)))
            {
                menu.AddItem(new GUIContent("Horizontal alignment/" + value), current.Horizontal == value,
                    () => ApplyFormat(horizontal: value));
            }
            foreach (VerticalCellAlignment value in Enum.GetValues(typeof(VerticalCellAlignment)))
            {
                menu.AddItem(new GUIContent("Vertical alignment/" + value), current.Vertical == value,
                    () => ApplyFormat(vertical: value));
            }
            menu.AddItem(new GUIContent("Wrap text"), current.WrapText, () => ApplyFormat(wrapText: !current.WrapText));
        }

        private void ApplyFormat(HorizontalCellAlignment? horizontal = null, VerticalCellAlignment? vertical = null,
            bool? wrapText = null)
        {
            Change(() =>
            {
                if (!_document.IsCsv)
                {
                    _document.SetCellFormat(MinRow, MinColumn, MaxRow, MaxColumn, horizontal, vertical, wrapText);
                    return;
                }

                for (int row = MinRow; row <= MaxRow; row++)
                {
                    for (int column = MinColumn; column <= MaxColumn; column++)
                    {
                        var entry = _csvLayout.cellFormats.Find(item => item.row == row && item.column == column);
                        if (entry == null)
                        {
                            entry = new CsvCellFormat { row = row, column = column, format = CellFormat.Default };
                            _csvLayout.cellFormats.Add(entry);
                        }
                        CellFormat format = entry.format;
                        format.Horizontal = horizontal ?? format.Horizontal;
                        format.Vertical = vertical ?? format.Vertical;
                        format.WrapText = wrapText ?? format.WrapText;
                        entry.format = format;
                    }
                }
            }, "Format spreadsheet cells", !_document.IsCsv);
        }

        private void ResizeSelection(float? width, float? height)
        {
            Change(() =>
            {
                if (width.HasValue)
                {
                    for (int column = MinColumn; column <= MaxColumn; column++)
                    {
                        SetColumnWidth(column, width.Value);
                    }
                }
                if (height.HasValue)
                {
                    for (int row = MinRow; row <= MaxRow; row++)
                    {
                        SetRowHeight(row, height.Value);
                    }
                }
            }, "Resize selected spreadsheet cells", !_document.IsCsv);
        }

        // 先根据内容调整列宽，再用新的列宽计算换行后的行高；整个过程只占一个 Undo 步骤。
        private void AutoFit(bool entireSheet)
        {
            int firstColumn = entireSheet ? 0 : MinColumn;
            int lastColumn = entireSheet ? _document.ColumnCount - 1 : MaxColumn;
            int firstRow = entireSheet ? 0 : MinRow;
            int lastRow = entireSheet ? _document.RowCount - 1 : MaxRow;
            Change(() =>
            {
                for (int column = firstColumn; column <= lastColumn; column++)
                {
                    FitColumn(column);
                }
                for (int row = firstRow; row <= lastRow; row++)
                {
                    FitRow(row);
                }
            }, "Auto fit spreadsheet", !_document.IsCsv);
        }

        private void AutoFitHeader(int index, bool column)
        {
            int first = column ? MinColumn : MinRow;
            int last = column ? MaxColumn : MaxRow;
            if (index < first || index > last)
            {
                first = last = index;
            }
            Change(() =>
            {
                for (int item = first; item <= last; item++)
                {
                    if (column)
                    {
                        FitColumn(item);
                    }
                    else
                    {
                        FitRow(item);
                    }
                }
            }, column ? "Auto fit spreadsheet columns" : "Auto fit spreadsheet rows", !_document.IsCsv);
        }

        private void FitColumn(int column)
        {
            // 自适应也会从菜单回调执行，显式取得皮肤，不依赖某个窗口先初始化 EditorStyles。
            var style = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label)
            {
                wordWrap = false,
                fontSize = SpreadsheetSettings.instance.FontSize
            };
            float width = MinColumnWidth;
            for (int row = 0; row < _document.RowCount; row++)
            {
                string value = _document.GetText(row, column);
                foreach (string line in value.Split('\n'))
                {
                    width = Mathf.Max(width, style.CalcSize(new GUIContent(line)).x + 10);
                }
            }
            SetColumnWidth(column, width);
        }

        private void FitRow(int row)
        {
            var style = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label)
            {
                fontSize = SpreadsheetSettings.instance.FontSize
            };
            float height = MinRowHeight;
            for (int column = 0; column < _document.ColumnCount; column++)
            {
                CellFormat format = GetDisplayFormat(row, column);
                style.wordWrap = format.WrapText;
                string value = _document.GetText(row, column);
                height = Mathf.Max(height, style.CalcHeight(new GUIContent(value), ColumnWidth(column) - 8) + 6);
            }
            SetRowHeight(row, height);
        }
    }
}
