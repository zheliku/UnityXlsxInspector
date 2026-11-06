using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace XlsxInspector.Tests
{
    public partial class SpreadsheetInspectorTests
    {
        /// <summary>设置页用真实 UI Toolkit 字段接收 TooltipEvent，并通过颜色字段修改和保存项目设置。</summary>
        [UnityTest]
        public IEnumerator SettingsFieldsExposeTooltipsAndSaveSeparateCustomPalettes()
        {
            var settings = SpreadsheetSettings.instance;
            string saved = JsonUtility.ToJson(settings);
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"_colorScheme\":0}", settings);
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 900);
                SpreadsheetSettings.CreateProvider().activateHandler("", window.rootVisualElement);
                yield return SettleLayout(window);
                var field = window.rootVisualElement.Q<PropertyField>("_fontSize");
                Assert.That(field, Is.Not.Null);
                Assert.That(field.worldBound.width, Is.GreaterThan(0));
                using (var tooltip = TooltipEvent.GetPooled())
                {
                    tooltip.target = field.Q<SliderInt>().Q<Label>();
                    field.SendEvent(tooltip);
                    Assert.That(tooltip.tooltip, Does.Contain("Auto Fit"));
                }
                foreach (var item in window.rootVisualElement.Query<PropertyField>().ToList())
                {
                    // 顶层设置项必须整行可提供提示，嵌套颜色字段由 TooltipAttribute 提供各自说明。
                    if (item.name.StartsWith("_"))
                    {
                        Assert.That(item.tooltip, Is.Not.Empty, item.name);
                    }
                }

                var scheme = window.rootVisualElement.Q<PropertyField>("_colorScheme").Q<PopupField<string>>();
                scheme.value = "Plum";
                yield return SettleLayout(window);
                Assert.That(settings.GetPalette(false).Accent, Is.EqualTo(SpreadsheetPalette.Create(SpreadsheetColorScheme.Plum, false).Accent));
                scheme.value = "Custom";
                yield return SettleLayout(window);
                Assert.That(window.rootVisualElement.Q<PropertyField>("_customLight").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

                Color light = settings.GetPalette(false).Accent;
                var dark = window.rootVisualElement.Q<PropertyField>("_customDark");
                var foldout = dark.Q<Foldout>();
                foldout.value = true;
                yield return SettleLayout(window);
                var color = dark.Query<ColorField>().ToList().Find(item => item.bindingPath.EndsWith("._accent"));
                Assert.That(color, Is.Not.Null);
                color.value = Color.magenta;
                yield return SettleLayout(window);
                Assert.That(settings.GetPalette(true).Accent, Is.EqualTo(Color.magenta));
                Assert.That(settings.GetPalette(false).Accent, Is.EqualTo(light));
                Assert.That(File.ReadAllText("ProjectSettings/UnityXlsxInspector.asset"), Does.Contain("_customDark"));
            }
            finally
            {
                window.Close();
                JsonUtility.FromJsonOverwrite(saved, settings);
                typeof(SpreadsheetSettings).GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(settings, null);
            }
        }

        /// <summary>四套预设在深浅主题下均使用不同底色，并让文字在内容区和选区中保持足够对比。</summary>
        /// <param name="scheme">待验证的内置配色。</param>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PresetColorsRemainReadableInBothThemes(int scheme)
        {
            var light = SpreadsheetPalette.Create((SpreadsheetColorScheme)scheme, false);
            var dark = SpreadsheetPalette.Create((SpreadsheetColorScheme)scheme, true);
            Assert.That(light.Background, Is.Not.EqualTo(dark.Background));
            foreach (bool isDark in new[] { false, true })
            {
                var palette = isDark ? dark : light;
                foreach (var background in new[] { palette.Background, palette.Alternate, palette.Empty, palette.Header,
                    Color.Lerp(palette.Background, palette.Accent, isDark ? .34f : .23f),
                    Color.Lerp(palette.Header, palette.Accent, .38f) })
                {
                    float foreground = Luminance(palette.Text);
                    float behind = Luminance(background);
                    float contrast = (Mathf.Max(foreground, behind) + .05f) / (Mathf.Min(foreground, behind) + .05f);
                    Assert.That(contrast, Is.GreaterThanOrEqualTo(4.5f), scheme + " dark=" + isDark);
                }
            }
        }

        private static float Luminance(Color color)
        {
            Color linear = color.linear;
            return .2126f * linear.r + .7152f * linear.g + .0722f * linear.b;
        }
    }
}
