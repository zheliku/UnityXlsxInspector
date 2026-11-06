using System;

namespace XlsxInspector
{
    /// <summary>Inspector 支持的水平对齐方式，General 保留 Excel 的按数据类型自动对齐。</summary>
    internal enum HorizontalCellAlignment
    {
        General,
        Left,
        Center,
        Right
    }

    /// <summary>Inspector 支持的垂直对齐方式。</summary>
    internal enum VerticalCellAlignment
    {
        Top,
        Center,
        Bottom
    }

    /// <summary>网格使用的基本显示格式；同一结构用于 Excel 格式和 CSV 会话内的显示格式。</summary>
    [Serializable]
    internal struct CellFormat
    {
        /// <summary>水平对齐；General 根据文本、数字等数据类型决定方向。</summary>
        public HorizontalCellAlignment Horizontal;

        /// <summary>垂直对齐。</summary>
        public VerticalCellAlignment Vertical;

        /// <summary>是否在列宽不足时自动换行。</summary>
        public bool WrapText;

        /// <summary>没有显式格式时使用的 Excel 风格默认设置。</summary>
        internal static CellFormat Default => new CellFormat
        {
            Horizontal = HorizontalCellAlignment.General,
            Vertical = VerticalCellAlignment.Bottom
        };
    }
}
