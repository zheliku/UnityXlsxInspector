using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XlsxInspector
{
    // 使用原生 UI Toolkit 字段，使整个选项（标签和输入区）都参与悬浮提示，而不是依赖 IMGUI 标签热点。
    internal sealed partial class SpreadsheetSettings
    {
        /// <summary>注册项目设置页，使用序列化绑定直接编辑共享设置。</summary>
        /// <returns>Unity Project Settings 使用的提供器。</returns>
        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Unity Xlsx Inspector", SettingsScope.Project)
            {
                activateHandler = (_, root) => BuildSettings(root),
                keywords = new HashSet<string> { "xlsx", "csv", "spreadsheet", "width", "height", "preview", "color", "theme", "font", "tooltip", "scroll" }
            };
        }

        private static void BuildSettings(VisualElement root)
        {
            var settings = instance;
            var serialized = new SerializedObject(settings);
            var scroll = new ScrollView();
            scroll.style.paddingLeft = scroll.style.paddingRight = 12;
            scroll.style.paddingTop = scroll.style.paddingBottom = 8;
            root.Add(scroll);

            AddHeading(scroll, "Default layout");
            scroll.Add(new HelpBox("Layout defaults apply to files without a saved layout. The grid extends as you scroll; empty cells do not add rows or columns to the file.", HelpBoxMessageType.Info));
            AddSetting(scroll, serialized, "_columnWidth", "CSV column width", "Initial CSV column width in logical pixels. Existing saved sizes and workbook dimensions are kept.");
            AddSetting(scroll, serialized, "_rowHeight", "CSV row height", "Initial CSV row height in logical pixels. Display sizes are saved separately from CSV contents.");
            AddSetting(scroll, serialized, "_panelHeight", "Panel height", "Initial grid height in logical pixels. Drag the bar below the grid to resize an individual file. The available Inspector space limits the height.");
            AddSetting(scroll, serialized, "_previewRows", "Initial preview rows", "Initial number of editable grid rows. Scrolling and keyboard navigation extend the preview automatically; this does not add rows to the file.");
            AddSetting(scroll, serialized, "_previewColumns", "Initial preview columns", "Initial number of editable grid columns. Scrolling and keyboard navigation extend the preview automatically; this does not add columns to the file.");

            AddHeading(scroll, "Appearance");
            AddSetting(scroll, serialized, "_fontSize", "Grid font size", "Grid font size in logical pixels. Auto Fit measures text at this size. Workbook fonts are preserved.");
            AddSetting(scroll, serialized, "_alternateRows", "Alternating row colors", "Use alternating backgrounds inside the content range to make wide tables easier to follow.");
            AddSetting(scroll, serialized, "_colorScheme", "Color scheme", "Forest, Ocean, Plum and Graphite adapt to Unity's Dark or Light theme. Custom lets you edit a separate palette for each theme.");
            var custom = new VisualElement();
            scroll.Add(custom);
            AddSetting(custom, serialized, "_customLight", "Custom · Light theme", "Colors used in Custom mode when Unity uses the Light theme. Expand to edit backgrounds, text, grid lines, accent and search colors.");
            AddSetting(custom, serialized, "_customDark", "Custom · Dark theme", "Colors used in Custom mode when Unity uses the Dark theme. The other theme's colors stay unchanged.");
            custom.style.display = settings._colorScheme == SpreadsheetColorScheme.Custom ? DisplayStyle.Flex : DisplayStyle.None;
            AddSetting(scroll, serialized, "_cellTooltips", "Cell tooltips", "Show a cell's address, full content and editing shortcuts on hover. Settings and toolbar help remain available when this is off.");
            AddSetting(scroll, serialized, "_showHints", "Show shortcut hints", "Show a short help line below the grid. The content range status stays visible when this is off.");

            AddHeading(scroll, "Selection scrolling");
            AddSetting(scroll, serialized, "_scrollSpeed", "Scroll speed", "Speed multiplier when dragging a selection near an edge. Set to 0 to disable automatic scrolling. Normal scrolling remains available.");
            AddSetting(scroll, serialized, "_scrollEdge", "Edge distance", "Distance in logical pixels from each grid edge that starts selection scrolling. Moving further beyond an edge increases the speed.");
            // PropertyField 先将值写回 SerializedObject，事件冒泡到这里后再保存；不维护第二份设置数据。
            scroll.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                settings.SaveSettings();
                custom.style.display = settings._colorScheme == SpreadsheetColorScheme.Custom ? DisplayStyle.Flex : DisplayStyle.None;
            });
            scroll.Bind(serialized);
        }

        private static void AddHeading(VisualElement root, string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 12;
            label.style.marginBottom = 6;
            root.Add(label);
        }

        // tooltip 赋给整个 PropertyField；嵌套颜色字段还通过 TooltipAttribute 提供逐项说明。
        private static void AddSetting(VisualElement root, SerializedObject serialized, string name, string label, string tooltip)
        {
            var field = new PropertyField(serialized.FindProperty(name), label) { name = name, tooltip = tooltip };
            field.style.marginBottom = 3;
            root.Add(field);
        }
    }
}
