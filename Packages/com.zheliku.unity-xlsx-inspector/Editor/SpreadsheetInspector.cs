using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace XlsxInspector
{
    /// <summary>
    /// 在 Project 资源的 Inspector 中显示和编辑表格，保留 Unity 原有导入器及图标。
    /// </summary>
    /// <remarks>
    /// Excel 以 DefaultAsset 导入，CSV 以 TextAsset 导入；两种资源共用此界面。
    /// IMGUI 每次事件都会重新执行绘制方法，因此文件读取放在 OnEnable，
    /// 文档修改放在输入处理方法，绘制阶段只读取当前状态。
    /// </remarks>
    [CustomEditor(typeof(DefaultAsset))]
    [CanEditMultipleObjects]
    public partial class SpreadsheetInspector : Editor
    {
        // 布局常量均使用 Unity GUI 的逻辑像素，缩放由 Unity 自身处理。
        private const float HeaderHeight = 24; // 顶部列字母区域的高度。
        private const float RowHeaderWidth = 45; // 左侧行号区域的宽度。
        private const float MinColumnWidth = 40; // 保留足够空间，避免列无法重新选中。
        private const float MaxColumnWidth = 1600; // 手动调整列宽的上限。
        private const float MinRowHeight = 20; // 保证文本和调整边界仍然可见。
        private const float MaxRowHeight = 500; // 手动调整行高的上限。
        private const string FormulaControl = "XlsxInspector.Formula"; // 上方编辑栏的 GUI 控件名称。
        private const string InlineControl = "XlsxInspector.Inline"; // 单元格内编辑器的 GUI 控件名称。

        // 文档及文件状态：资源路径交给 Unity，绝对路径交给 System.IO。
        private SpreadsheetDocument _document; // 当前加载的工作簿或 CSV。
        private Editor _defaultInspector; // 非表格资源继续使用 Unity 原生 Inspector。
        private string _assetPath; // Unity 资源路径，例如 Assets/Art/ProfileConfig.xlsx。
        private string _filePath; // 原文件的绝对路径。
        private string _draftPath; // Library 下的草稿文件路径。
        private string _layoutKey; // 当前资源的会话内 Undo 状态存储键。
        private string _error; // 最近一次操作失败时显示的错误说明。
        private bool _writable; // 原文件和包来源是否允许写入。

        // 选择及编辑状态：选区终点同时是当前活动单元格，坐标 x 表示列、y 表示行。
        private Vector2Int _anchor; // Shift 选择区域的固定起点。
        private Vector2Int _selected; // 当前活动单元格及区域选择的终点。
        private string _editBuffer = ""; // 编辑栏和格内编辑器共用的原始文本。
        private bool _editChanged; // 文本尚未提交到文档时为 true。
        private bool _editingInline; // 是否正在单元格内部编辑文本。
        private bool _focusInline; // 新建格内控件后，在下一次绘制时把键盘焦点交给它。
        private string _search = ""; // 当前 Sheet 的搜索关键词。

        // 网格状态：前缀坐标数组可快速定位任意尺寸的行列及滚动位置。
        private Vector2 _scroll; // 当前网格的水平和垂直滚动距离。
        private Vector2 _viewportSize; // 可见数据区域的大小，不包括固定表头。
        private float[] _columnEdges = Array.Empty<float>(); // 每列左边界，最后一项为整个网格宽度。
        private float[] _rowEdges = Array.Empty<float>(); // 每行上边界，最后一项为整个网格高度。
        private int _rows; // 已分配显示空间的行数，包含按需延伸的空白预览。
        private int _columns; // 已分配显示空间的列数，包含尾部可编辑空白列。
        private GUIStyle _cellStyle; // 可换行的单元格文本样式，在 OnGUI 中创建。
        private GUIStyle _headerStyle; // 去掉固定高度的表头样式，确保高行的行号背景完整铺满。
        private CsvLayout _csvLayout = new CsvLayout(); // CSV 尺寸只属于显示状态，不写进 CSV 数据。
        private Vector2 _sheetScroll; // 底部 Sheet 标签的水平滚动位置。
        private float _gridHeight; // 用户拖动设置的网格面板高度。
        private bool _resizingPanel; // 是否正在拖动网格底部的高度调整条。
        private float _panelPointer; // 开始调整面板高度时的鼠标位置。
        private float _panelOriginalHeight; // 开始调整面板高度时的原始高度。

        // 拖动尺寸时保留起始位置，避免每帧累加鼠标差值造成误差。
        private int _resizeIndex = -1; // 正在调整的行或列；-1 表示没有拖动。
        private bool _resizeColumn; // true 调整列宽，false 调整行高。
        private float _resizePointer; // 拖动开始时的鼠标坐标。
        private float _resizeOriginalSize; // 拖动开始前的尺寸。
        private int _resizeFirst; // 本次拖动批量调整的第一行或第一列。
        private int _resizeLast; // 本次拖动批量调整的最后一行或最后一列。
        private bool _resizeHasHistory; // 首次真实拖动时才记录 Undo，单击边界不会生成修改。

        [Serializable]
        private sealed class Draft
        {
            // JsonUtility 需要可序列化字段；名称对应已有草稿格式，不作为包的公开 API。
            public string sourceHash; // 草稿基于哪个版本的原文件。
            public string data; // 文档字节的 Base64，保留所有 Sheet 和格式。
            public int sheet; // 恢复时显示的工作表。
        }

        [Serializable]
        private sealed class CsvLayout
        {
            public float[] columnWidths = Array.Empty<float>(); // 当前会话内各列的显示宽度。
            public float[] rowHeights = Array.Empty<float>(); // 当前会话内各行的显示高度。
            public List<CsvCellFormat> cellFormats = new List<CsvCellFormat>(); // CSV 单元格的会话内格式。
        }

        [Serializable]
        private sealed class CsvCellFormat
        {
            public int row; // 格式对应的行索引。
            public int column; // 格式对应的列索引。
            public CellFormat format; // 水平与垂直对齐，以及自动换行。
        }

        // 简单、无副作用的计算属性使用表达式体；多步骤方法保留完整大括号。
        private int MinRow => Math.Min(_anchor.y, _selected.y);
        private int MaxRow => Math.Max(_anchor.y, _selected.y);
        private int MinColumn => Math.Min(_anchor.x, _selected.x);
        private int MaxColumn => Math.Max(_anchor.x, _selected.x);

        // Inspector 生命周期：打开时加载一次，切换资源或脚本重编译前保存草稿并释放文档。
        /// <summary>Unity 创建此 Inspector 时调用，确定文件位置、写入权限并恢复本地草稿。</summary>
        protected virtual void OnEnable()
        {
            _assetPath = AssetDatabase.GetAssetPath(target);
            string extension = Path.GetExtension(_assetPath);
            bool spreadsheet = extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
            if (targets.Length != 1 || !spreadsheet)
            {
                string type = target is TextAsset ? "UnityEditor.TextAssetInspector" : "UnityEditor.DefaultAssetInspector";
                _defaultInspector = CreateEditor(targets, typeof(Editor).Assembly.GetType(type));
                return;
            }

            string projectPath = Path.GetDirectoryName(Application.dataPath);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(_assetPath);
            _filePath = package == null ? Path.Combine(projectPath, _assetPath)
                : Path.Combine(package.resolvedPath, _assetPath.Substring(package.assetPath.Length + 1));
            string guid = AssetDatabase.AssetPathToGUID(_assetPath);
            _draftPath = Path.Combine(projectPath, "Library", "XlsxInspector", guid + ".json");
            _layoutKey = "XlsxInspector.Layout." + guid;
            LoadViewLayout(guid);
            _writable = (package == null || package.source == PackageSource.Embedded || package.source == PackageSource.Local)
                && !new FileInfo(_filePath).IsReadOnly;
            Run(Load);
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private static CsvLayout DeserializeCsvLayout(string json)
        {
            var layout = json.Length == 0 ? new CsvLayout() : JsonUtility.FromJson<CsvLayout>(json);
            // 旧版本的 SessionState 没有单元格格式字段，保留已有尺寸并补上新的空列表。
            layout.cellFormats ??= new List<CsvCellFormat>();
            return layout;
        }

        /// <summary>Unity 关闭 Inspector 时提交编辑、保留草稿，并关闭工作簿使用的数据流。</summary>
        protected virtual void OnDisable()
        {
            EndSelectionDrag();
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_defaultInspector != null)
            {
                DestroyImmediate(_defaultInspector);
                return;
            }

            if (_document == null)
            {
                return;
            }

            Run(() =>
            {
                CommitEdit();
                StoreDraft();
                StoreCsvLayout();
                StoreViewLayout();
                if (_resizeHasHistory)
                {
                    CaptureHistory();
                }
            });
            _document.Dispose();
            _document = null;
        }

        private void Load()
        {
            _document?.Dispose();
            _document = null;
            if (File.Exists(_draftPath))
            {
                var draft = JsonUtility.FromJson<Draft>(File.ReadAllText(_draftPath));
                _document = new SpreadsheetDocument(_filePath, Convert.FromBase64String(draft.data), draft.sourceHash);
                _document.SheetIndex = Mathf.Clamp(draft.sheet, 0, _document.SheetNames.Length - 1);
            }
            else
            {
                _document = new SpreadsheetDocument(_filePath);
            }

            int viewSheet = EditorPrefs.HasKey(_viewKey + ".view") ? _viewLayout.sheet
                : SessionState.GetInt(_layoutKey + ".sheet", _document.SheetIndex);
            InitializeHistory();
            _document.SheetIndex = Mathf.Clamp(viewSheet, 0, _document.SheetNames.Length - 1);
            SessionState.SetInt(_layoutKey + ".sheet", _document.SheetIndex);
            _anchor = _selected = Vector2Int.zero;
            _scroll = Vector2.zero;
            _editChanged = _editingInline = _focusInline = false;
            _editBuffer = _document.GetText(0, 0, true);
            _rows = _columns = 0;
            RebuildGrid();
            RestoreSheetView();
        }

        /// <summary>绘制表格，并处理当前 GUI 事件。此方法由 Unity 的 Inspector 自动调用。</summary>
        /// <remarks>
        /// Unity 默认禁用 DefaultAsset 的外层 GUI。这里临时启用自己的文件编辑区域，
        /// 再由文件权限控制可编辑按钮，结束后恢复调用者原本的 GUI 状态。
        /// </remarks>
        public override void OnInspectorGUI()
        {
            if (_defaultInspector != null)
            {
                _defaultInspector.OnInspectorGUI();
                return;
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = true;
            try
            {
                DrawSpreadsheet();
            }
            finally
            {
                GUI.enabled = previousEnabled;
            }
        }

        /// <summary>非表格资源继续使用 Unity 原有的预览区域。</summary>
        /// <returns>Unity 原生 Inspector 是否提供预览。</returns>
        public override bool HasPreviewGUI()
        {
            return _defaultInspector != null && _defaultInspector.HasPreviewGUI();
        }

        /// <summary>将非表格资源的预览绘制交给 Unity 原生 Inspector。</summary>
        /// <param name="rect">Unity 分配的预览区域。</param>
        /// <param name="background">Unity 当前主题的预览背景样式。</param>
        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            _defaultInspector?.OnPreviewGUI(rect, background);
        }

        /// <summary>将非表格资源的预览设置交给 Unity 原生 Inspector。</summary>
        public override void OnPreviewSettings()
        {
            _defaultInspector?.OnPreviewSettings();
        }

        // 保存边界：只有 Save 写入原件；草稿保存到 Library，Reload 由用户明确放弃草稿。
        private void Save()
        {
            if (!_writable)
            {
                return;
            }
            Run(() =>
            {
                CommitEdit();
                _editingInline = false;
                _document.Save();
                _historySourceHash = _document.SourceHash;
                _savedRevision = _historyState.Revision;
                CaptureHistory();
                if (File.Exists(_draftPath))
                {
                    File.Delete(_draftPath);
                }
                AssetDatabase.ImportAsset(_assetPath);
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.editingTextField = false;
            });
        }

        private void Reload()
        {
            if ((_document.IsDirty || _editChanged)
                && !EditorUtility.DisplayDialog("Discard spreadsheet edits?",
                    "Reload replaces your unsaved edits with the file on disk.", "Discard and Reload", "Cancel"))
            {
                return;
            }
            Run(() =>
            {
                if (File.Exists(_draftPath))
                {
                    File.Delete(_draftPath);
                }
                ClearHistory();
                Load();
                GUI.FocusControl(null);
            });
        }

        private void StoreDraft()
        {
            if (!_document.IsDirty)
            {
                if (File.Exists(_draftPath))
                {
                    File.Delete(_draftPath);
                }
                return;
            }
            var draft = new Draft
            {
                sourceHash = _document.SourceHash,
                data = Convert.ToBase64String(_document.GetBytes()),
                sheet = _document.SheetIndex
            };
            Directory.CreateDirectory(Path.GetDirectoryName(_draftPath));
            File.WriteAllText(_draftPath, JsonUtility.ToJson(draft));
        }

        private void Run(Action action)
        {
            try
            {
                _error = null;
                action();
            }
            catch (ExitGUIException)
            {
                // ExitGUIException 是 Unity 中断当前 GUI 绘制的信号，必须交回 Unity 处理。
                throw;
            }
            catch (Exception exception)
            {
                // 文件和剪贴板是外部输入边界：保留当前文档，并把可诊断的错误显示给用户。
                _error = exception.Message;
                Debug.LogException(exception, target);
            }
        }
    }
}
