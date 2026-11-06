using System;
using UnityEngine;

namespace XlsxInspector
{
    /// <summary>内置配色名称；每套预设都根据 Unity 的深浅主题选择对应底色。</summary>
    internal enum SpreadsheetColorScheme
    {
        Forest,
        Ocean,
        Plum,
        Graphite,
        Custom
    }

    /// <summary>网格使用的一组颜色。自定义模式分别保存浅色与深色版本。</summary>
    [Serializable]
    internal struct SpreadsheetPalette
    {
        [SerializeField, ColorUsage(false), Tooltip("Background of cells inside the content range.")]
        private Color _background; // 有内容范围内的基础底色。
        [SerializeField, ColorUsage(false), Tooltip("Background of alternating content rows when alternating colors are enabled.")]
        private Color _alternate; // 交替行底色。
        [SerializeField, ColorUsage(false), Tooltip("Background of empty cells beyond the content range. Keep it distinct from the content background.")]
        private Color _empty; // 内容范围以外空格的底色。
        [SerializeField, ColorUsage(false), Tooltip("Background of row and column headers, worksheet tabs and the resize handle.")]
        private Color _header; // 表头与底部标签的底色。
        [SerializeField, ColorUsage(false), Tooltip("Color of the grid lines between cells.")]
        private Color _grid; // 行列间的细网格线。
        [SerializeField, ColorUsage(false), Tooltip("Cell and header text color. Choose a color that is readable against the backgrounds above.")]
        private Color _text; // 同时作用于单元格和表头的文字颜色。
        [SerializeField, ColorUsage(false), Tooltip("Selection outline, selected headers, content boundary and active worksheet marker.")]
        private Color _accent; // 强调色；选区底色由它和基础底色混合得到。
        [SerializeField, ColorUsage(false), Tooltip("Marker beside cells matching the current search.")]
        private Color _search; // 搜索结果的侧边标记。

        /// <summary>内容区基础底色。</summary>
        internal Color Background => _background;
        /// <summary>交替行底色。</summary>
        internal Color Alternate => _alternate;
        /// <summary>内容范围以外的空格底色。</summary>
        internal Color Empty => _empty;
        /// <summary>表头和标签底色。</summary>
        internal Color Header => _header;
        /// <summary>单元格分隔线颜色。</summary>
        internal Color Grid => _grid;
        /// <summary>单元格及表头文字颜色。</summary>
        internal Color Text => _text;
        /// <summary>选区与活动标签的强调色。</summary>
        internal Color Accent => _accent;
        /// <summary>搜索结果标记颜色。</summary>
        internal Color Search => _search;

        // 预设共用一套亮度关系，强调色只轻微影响底色，保证深浅主题下文字仍清楚。
        /// <summary>生成指定预设和 Unity 主题对应的完整配色。</summary>
        /// <param name="scheme">内置配色；Custom 的初始颜色沿用 Forest。</param>
        /// <param name="dark">为 true 时生成深色主题配色。</param>
        /// <returns>可直接用于网格绘制或作为自定义起点的颜色值。</returns>
        internal static SpreadsheetPalette Create(SpreadsheetColorScheme scheme, bool dark)
        {
            Color accent;
            switch (scheme)
            {
                case SpreadsheetColorScheme.Ocean:
                    accent = dark ? new Color(.36f, .64f, .94f) : new Color(.18f, .46f, .78f);
                    break;
                case SpreadsheetColorScheme.Plum:
                    accent = dark ? new Color(.72f, .55f, .91f) : new Color(.56f, .33f, .73f);
                    break;
                case SpreadsheetColorScheme.Graphite:
                    accent = dark ? new Color(.61f, .69f, .79f) : new Color(.38f, .45f, .55f);
                    break;
                default:
                    accent = dark ? new Color(.34f, .76f, .59f) : new Color(.16f, .58f, .41f);
                    break;
            }
            Color background = Color.Lerp(dark ? new Color(.19f, .20f, .22f) : new Color(.98f, .98f, .98f), accent, .025f);
            return new SpreadsheetPalette
            {
                _background = background,
                _alternate = Color.Lerp(background, dark ? Color.white : Color.black, .035f),
                _empty = Color.Lerp(dark ? new Color(.12f, .13f, .15f) : new Color(.86f, .88f, .89f), accent, .04f),
                _header = Color.Lerp(dark ? new Color(.15f, .16f, .18f) : new Color(.88f, .90f, .91f), accent, .055f),
                _grid = dark ? new Color(.10f, .12f, .14f) : new Color(.77f, .80f, .82f),
                _text = dark ? new Color(.87f, .89f, .91f) : new Color(.12f, .15f, .18f),
                _accent = accent,
                _search = dark ? new Color(1, .77f, .30f) : new Color(.84f, .55f, .08f)
            };
        }
    }
}
