using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace XlsxInspector
{
    // 匹配与替换统一交给 .NET Regex；普通文本先 Escape，避免维护两套大小写和全词规则。
    public partial class SpreadsheetInspector
    {
        private const string SearchControl = "XlsxInspector.Search"; // 查找输入框的焦点名称。
        private const string ReplaceControl = "XlsxInspector.Replace"; // 替换输入框的焦点名称。
        private string _search = ""; // 当前 Sheet 的搜索表达式，关闭查找栏时保留输入。
        private string _replacement = ""; // 替换文本；正则模式支持 .NET 的 $1、${name} 等组引用。
        private bool _showSearch = true; // 是否显示查找栏，工具栏 Find 或 Ctrl/Cmd+F 可重新打开。
        private bool _showReplace; // 是否展开替换行。
        private bool _matchCase; // 为 true 时区分大小写。
        private bool _wholeWord; // 使用 Unicode 单词边界进行全词匹配。
        private bool _useRegex; // 为 true 时把输入作为正则表达式。
        private bool _searchSelection; // 是否将搜索限制在开启时选中的矩形内。
        private RectInt _searchRange; // 搜索选区快照；跳转结果不会改变搜索范围。
        private bool _focusSearch; // 在下一次绘制注册输入框后请求键盘焦点。
        private Regex _searchExpression; // 当前表达式，包含大小写、全词规则和超时时间。
        private string _searchError; // 无效表达式或超时的说明，只在查找栏显示。
        private readonly List<Vector2Int> _searchMatches = new List<Vector2Int>(); // 按行列顺序排列的匹配单元格。
        private readonly HashSet<Vector2Int> _searchCells = new HashSet<Vector2Int>(); // 网格标记使用的常数时间位置查询。
        private (SpreadsheetDocument Document, int Sheet, int Step, string Text, bool Case, bool Word, bool Regex, bool Selection, RectInt Range) _searchKey; // 文档和选项未变化时复用结果。

        // 工具栏和快捷键共用入口，先提交当前格，再在下一次重绘中聚焦搜索框。
        private void OpenSearch(bool replace = false)
        {
            Run(CommitEdit);
            _editingInline = false;
            _showSearch = _focusSearch = true;
            _showReplace = replace || _showReplace;
            Repaint();
        }

        private void RefreshSearch()
        {
            var key = (Document: _document, Sheet: _document.SheetIndex, Step: _historyState.Step,
                Text: _showSearch ? _search : "", Case: _matchCase, Word: _wholeWord,
                Regex: _useRegex, Selection: _searchSelection, Range: _searchRange);
            if (_searchKey.Equals(key))
            {
                return;
            }
            _searchKey = key;
            _searchMatches.Clear();
            _searchCells.Clear();
            _searchExpression = null;
            _searchError = null;
            if (key.Text.Length == 0)
            {
                return;
            }

            try
            {
                string pattern = _useRegex ? _search : Regex.Escape(_search);
                if (_wholeWord)
                {
                    pattern = @"(?<!\w)(?:" + pattern + @")(?!\w)";
                }
                RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Multiline;
                if (!_matchCase)
                {
                    options |= RegexOptions.IgnoreCase;
                }
                _searchExpression = new Regex(pattern, options, TimeSpan.FromMilliseconds(100));
                int firstRow = _searchSelection ? _searchRange.yMin : 0;
                int firstColumn = _searchSelection ? _searchRange.xMin : 0;
                int rows = _searchSelection ? Math.Min(_dataRows, _searchRange.yMax) : _dataRows;
                int columns = _searchSelection ? Math.Min(_dataColumns, _searchRange.xMax) : _dataColumns;
                for (int row = firstRow; row < rows; row++)
                {
                    for (int column = firstColumn; column < columns; column++)
                    {
                        if (_searchExpression.IsMatch(_document.GetText(row, column, true)))
                        {
                            var cell = new Vector2Int(column, row);
                            _searchMatches.Add(cell);
                            _searchCells.Add(cell);
                        }
                    }
                }
            }
            catch (ArgumentException exception)
            {
                SetSearchError(exception.Message);
            }
            catch (RegexMatchTimeoutException)
            {
                SetSearchError("The expression took too long to match. Simplify the pattern.");
            }
        }

        private void SetSearchError(string message)
        {
            _searchError = message;
            _searchMatches.Clear();
            _searchCells.Clear();
        }

        private void FindNext()
        {
            FindMatch(false);
        }

        // 导航以单元格为单位；最后一个结果继续向后时回到第一个，向前导航采用相同规则。
        private void FindMatch(bool previous)
        {
            Run(() =>
            {
                CommitEdit();
                RefreshSearch();
                if (_searchMatches.Count == 0)
                {
                    return;
                }
                int index = _searchMatches.IndexOf(_selected);
                if (index >= 0)
                {
                    index = (index + (previous ? _searchMatches.Count - 1 : 1)) % _searchMatches.Count;
                }
                else
                {
                    index = previous ? _searchMatches.FindLastIndex(cell => cell.y < _selected.y || cell.y == _selected.y && cell.x < _selected.x)
                        : _searchMatches.FindIndex(cell => cell.y > _selected.y || cell.y == _selected.y && cell.x > _selected.x);
                    if (index < 0)
                    {
                        index = previous ? _searchMatches.Count - 1 : 0;
                    }
                }
                Vector2Int match = _searchMatches[index];
                Select(match.y, match.x, false);
                RevealSelection();
                Repaint();
            });
        }

        private void ReplaceMatches(bool all)
        {
            // Enter 也能从替换框进入此方法，不能只依赖按钮的 DisabledScope 限制只读资源。
            if (!_writable)
            {
                return;
            }
            Run(() =>
            {
                CommitEdit();
                RefreshSearch();
                if (_searchExpression == null || _searchMatches.Count == 0 || _searchError != null)
                {
                    return;
                }
                if (!all && !_searchCells.Contains(_selected))
                {
                    FindNext();
                }
                // 先计算所有替换结果，再交给既有的单步 Undo 入口；表达式出错时不留下半次批量修改。
                var edits = new List<(Vector2Int Cell, string Value)>();
                try
                {
                    foreach (Vector2Int cell in _searchMatches)
                    {
                        if (!all && cell != _selected)
                        {
                            continue;
                        }
                        string original = _document.GetText(cell.y, cell.x, true);
                        string value = _useRegex ? _searchExpression.Replace(original, _replacement)
                            : _searchExpression.Replace(original, _ => _replacement);
                        if (original != value)
                        {
                            edits.Add((cell, value));
                        }
                    }
                }
                catch (ArgumentException exception)
                {
                    SetSearchError(exception.Message);
                    return;
                }
                catch (RegexMatchTimeoutException)
                {
                    SetSearchError("The expression took too long to replace. Simplify the pattern.");
                    return;
                }
                if (edits.Count > 0)
                {
                    Change(() =>
                    {
                        foreach (var edit in edits)
                        {
                            _document.SetText(edit.Cell.y, edit.Cell.x, edit.Value);
                        }
                    }, all ? "Replace all spreadsheet matches" : "Replace spreadsheet match");
                    RefreshSearch();
                }
                if (!all)
                {
                    FindNext();
                }
            });
        }
    }
}
