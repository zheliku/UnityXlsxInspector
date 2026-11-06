using System;
using System.Collections;
using System.Collections.Generic;
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
        private void OpenSearchTable()
        {
            Open(_csvPath);
            Invoke("Change", (Action)(() =>
            {
                Document.SetText(0, 0, "Cat cat scatter");
                Document.SetText(0, 1, "CAT");
                Document.SetText(1, 0, "cat_1");
                Document.SetText(1, 1, "dog 123\ncat 456");
            }), "Prepare search cells", true);
            SetField("_search", "cat");
        }

        /// <summary>大小写、全词和正则采用同一匹配入口，结果按单元格计数并支持换行内容。</summary>
        [Test]
        public void SearchSupportsCaseWholeWordRegexAndMultilineContent()
        {
            OpenSearchTable();
            Invoke("RefreshSearch");
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Has.Count.EqualTo(4));
            SetField("_matchCase", true);
            Invoke("RefreshSearch");
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Has.Count.EqualTo(3));
            SetField("_wholeWord", true);
            Invoke("RefreshSearch");
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Has.Count.EqualTo(2));
            SetField("_useRegex", true);
            SetField("_search", @"^cat \d+$");
            Invoke("RefreshSearch");
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Is.EqualTo(new[] { new Vector2Int(1, 1) }));
            Assert.That(GetField<HashSet<Vector2Int>>("_searchCells"), Contains.Item(new Vector2Int(1, 1)));
        }

        /// <summary>向前、向后查找循环跳转；选区搜索固定开启时的范围，不被结果导航改变。</summary>
        [Test]
        public void SearchNavigationWrapsAndKeepsSelectionScope()
        {
            OpenSearchTable();
            Invoke("Select", 0, 0, false);
            Invoke("FindNext");
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(1, 0)));
            Invoke("FindMatch", true);
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(Vector2Int.zero));
            Invoke("FindMatch", true);
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(1, 1)));
            SetField("_searchSelection", true);
            SetField("_searchRange", new RectInt(0, 0, 1, 2));
            Invoke("RefreshSearch");
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Is.EqualTo(new[] { Vector2Int.zero, new Vector2Int(0, 1) }));
            Invoke("FindNext");
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(Vector2Int.zero));
        }

        /// <summary>替换全部在一个 Undo 步骤内完成，支持正则分组，并随撤销、重做、Sheet 切换刷新结果。</summary>
        /// <param name="excel">为 true 时验证 XLSX，否则验证 CSV。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void RegexReplacementIsOneUndoAndResultsFollowDocumentChanges(bool excel)
        {
            Open(excel ? _xlsxPath : _csvPath);
            Invoke("Change", (Action)(() =>
            {
                Document.SetText(0, 0, "item-12");
                Document.SetText(1, 0, "item-34 item-56");
            }), "Prepare replace cells", true);
            SetField("_search", @"item-(\d+)");
            SetField("_useRegex", true);
            SetField("_replacement", "entry-$1");
            Invoke("ReplaceMatches", true);
            Assert.That(Document.GetText(1, 0), Is.EqualTo("entry-34 entry-56"));
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Is.Empty);
            Undo.PerformUndo();
            Invoke("RefreshSearch");
            Assert.That(Document.GetText(1, 0), Is.EqualTo("item-34 item-56"));
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Has.Count.EqualTo(2));
            Undo.PerformRedo();
            Invoke("RefreshSearch");
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Is.Empty);
            if (excel)
            {
                SetField("_search", "entry");
                Invoke("SwitchSheet", 1);
                Invoke("RefreshSearch");
                Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Is.Empty);
                Invoke("SwitchSheet", 0);
                Invoke("RefreshSearch");
                Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Has.Count.EqualTo(2));
            }
        }

        /// <summary>普通替换保留 $ 字面量；无效正则只显示查找错误，不影响网格或原文件。</summary>
        [Test]
        public void LiteralReplacementKeepsDollarSignsAndInvalidRegexIsReported()
        {
            OpenSearchTable();
            SetField("_replacement", "$1");
            Invoke("ReplaceMatches", true);
            Assert.That(Document.GetText(0, 1), Is.EqualTo("$1"));
            SetField("_useRegex", true);
            SetField("_search", "[");
            Invoke("RefreshSearch");
            Assert.That(GetField<string>("_searchError"), Is.Not.Empty);
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Is.Empty);
            Assert.That(GetField<string>("_error"), Is.Null.Or.Empty);
            SetField("_search", @"\$1");
            Invoke("RefreshSearch");
            Assert.That(GetField<string>("_searchError"), Is.Null);
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Has.Count.EqualTo(4));
        }

        /// <summary>只读资源允许搜索导航，键盘进入的替换流程也不能改变内容。</summary>
        [Test]
        public void ReadOnlySearchAllowsNavigationButCannotReplace()
        {
            OpenSearchTable();
            SetField("_writable", false);
            SetField("_replacement", "changed");
            Invoke("FindNext");
            Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(1, 0)));
            Invoke("ReplaceMatches", false);
            Invoke("ReplaceMatches", true);
            Assert.That(Document.GetText(0, 1), Is.EqualTo("CAT"));
            Assert.That(Document.GetText(0, 0), Is.EqualTo("Cat cat scatter"));
        }

        /// <summary>用户输入的灾难性回溯表达式由 .NET 超时终止，错误不污染文档或阻塞后续搜索。</summary>
        [Test]
        public void RegexTimeoutDoesNotBlockLaterQueries()
        {
            Open(_csvPath);
            Invoke("Change", (Action)(() => Document.SetText(0, 0, new string('a', 10000) + "!")), "Prepare regex timeout", true);
            SetField("_search", "(a+)+$");
            SetField("_useRegex", true);
            Invoke("RefreshSearch");
            Assert.That(GetField<string>("_searchError"), Does.Contain("too long"));
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Is.Empty);
            SetField("_search", "!");
            Invoke("RefreshSearch");
            Assert.That(GetField<string>("_searchError"), Is.Null);
            Assert.That(GetField<List<Vector2Int>>("_searchMatches"), Has.Count.EqualTo(1));
        }

        /// <summary>真实 Inspector 中 Ctrl+F 聚焦查找，Enter 导航，Esc 关闭，Ctrl+H 展开替换。</summary>
        [UnityTest]
        public IEnumerator SearchShortcutsWorkInTheInspector()
        {
            var window = ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();
            try
            {
                OpenSearchTable();
                window.ShowUtility();
                window.position = new Rect(40, 40, 800, 750);
                var scroll = new ScrollView { style = { flexGrow = 1 } };
                scroll.Add(new InspectorElement(_editor));
                window.rootVisualElement.Add(scroll);
                yield return SettleLayout(window);
                Invoke("Select", 0, 0, false);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.F, control = true });
                yield return SettleLayout(window);
                Assert.That(GetField<bool>("_showSearch"), Is.True);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, control = true });
                foreach (char letter in "CAT")
                {
                    window.SendEvent(new Event { type = EventType.KeyDown, character = letter });
                }
                yield return SettleLayout(window);
                Assert.That(GetField<string>("_search"), Is.EqualTo("CAT"));
                Assert.That(GetField<bool>("_editingInline"), Is.False);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                yield return SettleLayout(window);
                Assert.That(GetField<Vector2Int>("_selected"), Is.EqualTo(new Vector2Int(1, 0)));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                yield return SettleLayout(window);
                Assert.That(GetField<bool>("_showSearch"), Is.False);
                Assert.That(GetField<HashSet<Vector2Int>>("_searchCells"), Is.Empty);
                // 不使用快捷键也能重新打开搜索；入口必须恢复焦点，随后输入仍属于查找框。
                var inspector = GetField<IMGUIContainer>("_inspectorElement");
                Vector2 find = inspector.worldBound.position + GetField<Rect>("_findButtonRect").center;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = find });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = find });
                yield return SettleLayout(window);
                Assert.That(GetField<bool>("_showSearch"), Is.True);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, control = true });
                window.SendEvent(new Event { type = EventType.KeyDown, character = 'x' });
                yield return SettleLayout(window);
                Assert.That(GetField<string>("_search"), Is.EqualTo("x"));
                Assert.That(GetField<bool>("_editingInline"), Is.False);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.H, control = true });
                yield return SettleLayout(window);
                Assert.That(GetField<bool>("_showReplace"), Is.True);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
