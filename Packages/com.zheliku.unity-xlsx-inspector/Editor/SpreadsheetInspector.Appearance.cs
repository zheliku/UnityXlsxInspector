using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    // 这里只定义 Inspector 的显示方式，不改变文档格式或引入独立主题系统。
    public partial class SpreadsheetInspector
    {
        private GUIStyle _inlineStyle; // 格内编辑器使用与预览一致的字号，保留原生 TextArea 光标和背景。
        private SpreadsheetPalette _palette; // 当前一帧的配色，避免逐格重复计算预设颜色。

        // 各底色按当前 Unity 皮肤计算，切换皮肤时不需要清理缓存或重新打开资源。
        private Color GridBackground => _palette.Background;
        private Color GridLine => _palette.Grid;
        private Color HeaderBackground => _palette.Header;
        private Color EmptyBackground => _palette.Empty;
        private Color Accent => _palette.Accent;
        private Color SelectionBackground => Color.Lerp(GridBackground, Accent, EditorGUIUtility.isProSkin ? .34f : .23f);

        // 文本使用不带背景的原生 label；底色自己画满矩形，避免高行出现按钮贴图的深浅断层。
        private void PrepareAppearance()
        {
            _palette = SpreadsheetSettings.instance.GetPalette(EditorGUIUtility.isProSkin);
            _cellStyle ??= new GUIStyle(EditorStyles.label);
            _cellStyle.fontSize = SpreadsheetSettings.instance.FontSize;
            _cellStyle.normal.textColor = _palette.Text;
            _inlineStyle ??= new GUIStyle(EditorStyles.textArea);
            _inlineStyle.fontSize = SpreadsheetSettings.instance.FontSize;
            _headerStyle ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                padding = new RectOffset(2, 2, 0, 0)
            };
            _headerStyle.normal.textColor = _headerStyle.hover.textColor = _headerStyle.active.textColor = _palette.Text;
        }

        private bool DrawHeaderButton(Rect rect, string label, bool inTable, bool selected, string tooltip)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color background = selected ? Color.Lerp(HeaderBackground, Accent, .38f) : inTable ? HeaderBackground : EmptyBackground;
            EditorGUI.DrawRect(rect, hovered ? Color.Lerp(background, Accent, .15f) : background);
            Color previous = GUI.contentColor;
            if (!inTable && !selected)
            {
                GUI.contentColor = Color.Lerp(Color.white, _palette.Empty, .35f);
            }
            bool clicked = GUI.Button(rect, new GUIContent(label, tooltip), _headerStyle);
            GUI.contentColor = previous;
            return clicked;
        }

        // 四条细矩形围出当前格，选区内的其他格保持统一底色；不会覆盖文字或拦截事件。
        private void DrawActiveCellOutline(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), Accent);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Accent);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2, rect.height), Accent);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Accent);
        }
    }
}
