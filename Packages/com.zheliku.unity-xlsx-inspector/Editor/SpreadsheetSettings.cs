using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    /// <summary>团队共享的表格布局、显示与拖选参数。单个资源的个人布局由 Inspector 单独保存。</summary>
    [FilePath("ProjectSettings/UnityXlsxInspector.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed partial class SpreadsheetSettings : ScriptableSingleton<SpreadsheetSettings>
    {
        [SerializeField] private float _columnWidth = 120; // CSV 未设置列宽时使用的像素宽度。
        [SerializeField] private float _rowHeight = 24; // CSV 未设置行高时使用的像素高度。
        [SerializeField] private float _panelHeight = 440; // 首次打开表格时的网格面板高度。
        [SerializeField] private int _previewRows = 20; // 初始预览行数，滚动和键盘导航会自动延伸。
        [SerializeField] private int _previewColumns = 8; // 初始预览列数，不影响文件内容。
        [SerializeField, Range(10, 24)] private int _fontSize = 12; // 网格文字的逻辑像素大小，不修改工作簿字体。
        [SerializeField] private bool _alternateRows = true; // 使用交替行底色，方便横向阅读。
        [SerializeField] private SpreadsheetColorScheme _colorScheme = SpreadsheetColorScheme.Forest; // 当前选择的内置配色或自定义模式。
        [SerializeField] private SpreadsheetPalette _customLight = SpreadsheetPalette.Create(SpreadsheetColorScheme.Forest, false); // 浅色主题的个人配色。
        [SerializeField] private SpreadsheetPalette _customDark = SpreadsheetPalette.Create(SpreadsheetColorScheme.Forest, true); // 深色主题的个人配色。
        [SerializeField] private bool _cellTooltips = true; // 悬停单元格时显示地址、完整内容与编辑提示。
        [SerializeField] private bool _showHints = true; // 网格下方是否显示快捷操作说明。
        [SerializeField, Range(0, 3)] private float _scrollSpeed = 1; // 拖选自动滚动的速度倍率；0 关闭自动滚动。
        [SerializeField, Range(8, 64)] private float _scrollEdge = 24; // 距视口边缘多近时开始滚动，单位为逻辑像素。

        /// <summary>CSV 默认列宽，单位为 Unity GUI 逻辑像素。</summary>
        internal float ColumnWidth => _columnWidth;

        /// <summary>CSV 默认行高，单位为 Unity GUI 逻辑像素。</summary>
        internal float RowHeight => _rowHeight;

        /// <summary>没有个人布局时使用的初始面板高度。</summary>
        internal float PanelHeight => _panelHeight;

        /// <summary>初始预览行数；不改变文档的实际行数。</summary>
        internal int PreviewRows => _previewRows;

        /// <summary>初始预览列数；不改变文档的实际列数。</summary>
        internal int PreviewColumns => _previewColumns;

        /// <summary>网格显示字号；仅影响 Inspector，内容自适应使用同一个字号测量。</summary>
        internal int FontSize => _fontSize;

        /// <summary>是否为内容区的相邻行使用不同底色。</summary>
        internal bool AlternateRows => _alternateRows;

        /// <summary>是否在单元格悬浮提示中显示完整内容。</summary>
        internal bool CellTooltips => _cellTooltips;

        /// <summary>是否显示网格下方的快捷操作说明；数据范围状态始终显示。</summary>
        internal bool ShowHints => _showHints;

        /// <summary>拖选自动滚动的速度倍率；0 表示停用。</summary>
        internal float ScrollSpeed => _scrollSpeed;

        /// <summary>自动滚动的边缘感应距离，单位为逻辑像素。</summary>
        internal float ScrollEdge => _scrollEdge;

        /// <summary>取得指定 Unity 主题当前使用的配色。</summary>
        /// <param name="dark">为 true 时返回深色版本，否则返回浅色版本。</param>
        /// <returns>预设生成的颜色或用户保存的自定义配色。</returns>
        internal SpreadsheetPalette GetPalette(bool dark)
        {
            return _colorScheme == SpreadsheetColorScheme.Custom ? (dark ? _customDark : _customLight)
                : SpreadsheetPalette.Create(_colorScheme, dark);
        }

        // 保留输入边界校验；保存项目设置后只重绘界面，不改动文档或撤销历史。
        private void SaveSettings()
        {
            _columnWidth = Mathf.Clamp(_columnWidth, 40, 1600);
            _rowHeight = Mathf.Clamp(_rowHeight, 20, 500);
            _panelHeight = Mathf.Max(180, _panelHeight);
            _previewRows = Mathf.Clamp(_previewRows, 1, 1048576);
            _previewColumns = Mathf.Clamp(_previewColumns, 1, 16384);
            Save(true);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
