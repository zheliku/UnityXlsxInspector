using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    // Unity Undo 只记录 UnityEngine.Object 的序列化字段，所以使用一个隐藏的 ScriptableObject
    // 保存文档快照。Excel 文档本身仍由 SpreadsheetDocument 负责，不增加另一套编辑数据模型。
    public partial class SpreadsheetInspector
    {
        private SpreadsheetHistoryState _historyState; // 当前资源的 Undo 序列化桥接对象。
        private string _historySourceHash; // 当前磁盘版本，保存后更新，不随 Undo 回到旧版本。
        private int _savedRevision; // 最近一次保存对应的数据修订号。
        private int _nextRevision; // 单调递增的数据修订号，避免撤销后新编辑与旧分支重号。
        private int _nextHistoryStep; // 所有操作的单调序号，CSV 显示格式也单独占一个历史步骤。
        private string _appliedHistoryData; // 上次已应用的文档快照，用来忽略其他对象的 Undo 通知。
        private string _appliedHistoryLayout; // 上次已应用的 CSV 显示格式快照。
        private int _appliedHistoryRevision; // 上次已应用的数据修订号。
        private int _appliedHistoryStep; // 当前历史节点的操作序号，用于判断撤销或重做方向。
        private int _appliedHistorySheet; // 当前节点代表的操作发生在哪个 Sheet。
        private Vector2Int _appliedHistorySelected; // 当前节点代表的操作位置。
        private Vector2Int _appliedHistoryAnchor; // 当前节点代表的选区起点。

        // 历史对象在当前 Editor 会话中存活，资源切换只释放工作簿，不清除 Undo 链。
        private void InitializeHistory()
        {
            string guid = AssetDatabase.AssetPathToGUID(_assetPath);
            _historyState = Resources.FindObjectsOfTypeAll<SpreadsheetHistoryState>()
                .FirstOrDefault(state => state.AssetGuid == guid);
            _historySourceHash = SessionState.GetString(_layoutKey + ".source", _document.SourceHash);
            _savedRevision = SessionState.GetInt(_layoutKey + ".saved", _document.IsDirty ? -1 : 0);
            _nextRevision = SessionState.GetInt(_layoutKey + ".revision", 0);
            _nextHistoryStep = SessionState.GetInt(_layoutKey + ".step", 0);

            if (_historyState != null && !_document.IsDirty && _historySourceHash != _document.SourceHash)
            {
                // 文件在外部被改写后，旧历史不应覆盖新文件内容。
                ClearHistory();
            }

            if (_historyState == null)
            {
                _historyState = CreateInstance<SpreadsheetHistoryState>();
                _historyState.hideFlags = HideFlags.HideAndDontSave;
                _historyState.name = "Unity Xlsx Inspector History";
                _historyState.AssetGuid = guid;
                _historySourceHash = _document.SourceHash;
                _savedRevision = _document.IsDirty ? -1 : 0;
                _nextRevision = 0;
                _nextHistoryStep = 0;
                _historyState.Sheet = _document.SheetIndex;
                CaptureHistory();
            }
            else
            {
                RestoreHistory();
            }
        }

        private void BeginHistory(string name, bool affectsDocument = true)
        {
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(_historyState, name);
            _historyState.Step = ++_nextHistoryStep;
            _historyState.Sheet = _document.SheetIndex;
            _historyState.Selected = _selected;
            _historyState.Anchor = _anchor;
            if (affectsDocument)
            {
                _historyState.Revision = ++_nextRevision;
            }
        }

        private void CaptureHistory()
        {
            _historyState.Data = Convert.ToBase64String(_document.GetBytes());
            _historyState.Layout = JsonUtility.ToJson(_csvLayout);
            RememberHistoryPosition();
            SessionState.SetString(_layoutKey + ".source", _historySourceHash);
            SessionState.SetInt(_layoutKey + ".saved", _savedRevision);
            SessionState.SetInt(_layoutKey + ".revision", _nextRevision);
            SessionState.SetInt(_layoutKey + ".step", _nextHistoryStep);
        }

        private void OnUndoRedo()
        {
            if (_document == null || _historyState == null)
            {
                return;
            }

            if (_historyState.Data == _appliedHistoryData && _historyState.Layout == _appliedHistoryLayout
                && _historyState.Revision == _appliedHistoryRevision && _historyState.Step == _appliedHistoryStep)
            {
                return;
            }

            Run(() => RestoreHistory(true));
            Repaint();
        }

        private void PerformHistory(bool redo)
        {
            Run(() =>
            {
                CommitEdit();
                _editingInline = _focusInline = false;
                if (redo)
                {
                    Undo.PerformRedo();
                }
                else
                {
                    Undo.PerformUndo();
                }
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
            });
        }

        private void RestoreHistory(bool undoEvent = false)
        {
            // 撤销使用刚离开的操作目标，重做使用进入的操作目标；这样跨 Sheet 连续撤销时会显示正确位置。
            bool movingBack = undoEvent && _historyState.Step < _appliedHistoryStep;
            int sheet = movingBack ? _appliedHistorySheet : _historyState.Sheet;
            Vector2Int selected = movingBack ? _appliedHistorySelected : _historyState.Selected;
            Vector2Int anchor = movingBack ? _appliedHistoryAnchor : _historyState.Anchor;
            _document.Dispose();
            _document = new SpreadsheetDocument(_filePath, Convert.FromBase64String(_historyState.Data),
                _historySourceHash, _historyState.Revision != _savedRevision);
            _document.SheetIndex = Mathf.Clamp(sheet, 0, _document.SheetNames.Length - 1);
            _csvLayout = DeserializeCsvLayout(_historyState.Layout);
            _selected = selected;
            _anchor = anchor;
            _editChanged = _editingInline = _focusInline = false;
            RebuildGrid();
            _editBuffer = _document.GetText(_selected.y, _selected.x, true);
            RememberHistoryPosition();
            SessionState.SetInt(_layoutKey + ".sheet", _document.SheetIndex);
            StoreDraft();
            StoreCsvLayout();
            if (_viewportSize.x > 0 && _viewportSize.y > 0)
            {
                RevealSelection();
            }
        }

        private void RememberHistoryPosition()
        {
            _appliedHistoryData = _historyState.Data;
            _appliedHistoryLayout = _historyState.Layout;
            _appliedHistoryRevision = _historyState.Revision;
            _appliedHistoryStep = _historyState.Step;
            _appliedHistorySheet = _historyState.Sheet;
            _appliedHistorySelected = _historyState.Selected;
            _appliedHistoryAnchor = _historyState.Anchor;
        }

        private void ClearHistory()
        {
            if (_historyState != null)
            {
                Undo.ClearUndo(_historyState);
                DestroyImmediate(_historyState);
                _historyState = null;
            }
        }
    }
}
