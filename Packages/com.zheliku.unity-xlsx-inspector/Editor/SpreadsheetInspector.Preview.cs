using System;
using UnityEngine;

namespace XlsxInspector
{
    // 复用现有前缀坐标，接近边缘时补出一屏空格。扩展视图不调用文档写入，也不分配 CSV 格式数组。
    public partial class SpreadsheetInspector
    {
        private void EnsurePreviewAt(Vector2 position)
        {
            int row = _rows - 1;
            int column = _columns - 1;
            // 最小行列尺寸保证估算出来的空间足够，不需要在一帧中反复重建网格。
            if (position.y + _viewportSize.y / 2 >= _rowEdges[_rows])
            {
                row += Mathf.Max(1, Mathf.CeilToInt((position.y + _viewportSize.y - _rowEdges[_rows]) / MinRowHeight));
            }
            if (position.x + _viewportSize.x / 2 >= _columnEdges[_columns])
            {
                column += Mathf.Max(1, Mathf.CeilToInt((position.x + _viewportSize.x - _columnEdges[_columns]) / MinColumnWidth));
            }
            EnsurePreviewCell(row, column);
        }

        private void EnsurePreviewCell(int row, int column)
        {
            int rows = Math.Min(_document.MaxRows, Math.Max(_rows, row + 1));
            int columns = Math.Min(_document.MaxColumns, Math.Max(_columns, column + 1));
            if (rows == _rows && columns == _columns)
            {
                return;
            }
            _rows = rows;
            _columns = columns;
            RebuildGrid();
            _inspectorElement?.MarkDirtyRepaint();
            Repaint();
        }

        // 鼠标松开后，保留实际内容和视口外一屏，其余尾部灰格可以释放。选区仍保留文档坐标。
        // 不改原文件、格式或滚动位置，也不在拖动滚动条时改变它的比例。
        private void ReleaseUnusedPreview()
        {
            int rows = Math.Max(SpreadsheetSettings.instance.PreviewRows,
                Math.Max(_dataRows + 1, FindIndex(_rowEdges, _scroll.y + _viewportSize.y * 2) + 1));
            int columns = Math.Max(SpreadsheetSettings.instance.PreviewColumns,
                Math.Max(_dataColumns + 1, FindIndex(_columnEdges, _scroll.x + _viewportSize.x * 2) + 1));
            rows = Math.Min(rows, _document.MaxRows);
            columns = Math.Min(columns, _document.MaxColumns);
            if (rows >= _rows && columns >= _columns)
            {
                return;
            }
            _rows = rows;
            _columns = columns;
            RebuildGrid();
        }
    }
}
