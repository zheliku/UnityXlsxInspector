using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    // EditorPrefs 是个人本机设置，键包含项目路径和资源 GUID，不会与其他项目或团队成员混用。
    public partial class SpreadsheetInspector
    {
        private string _viewKey; // 当前项目和资源的持久化布局键。
        private ViewLayout _viewLayout = new ViewLayout(); // 文件面板及各 Sheet 的视图状态。

        [Serializable]
        private sealed class ViewLayout
        {
            public int sheet; // 最近查看的 Sheet。
            public List<SheetView> sheets = new List<SheetView>(); // 按 Sheet 名称保存，避免不同表的滚动互相覆盖。
        }

        [Serializable]
        private sealed class SheetView
        {
            public string name; // 工作表名称，CSV 使用文件名。
            public Vector2 scroll; // 网格的水平和垂直滚动距离。
        }

        private void LoadViewLayout(string guid)
        {
            _viewKey = "UnityXlsxInspector." + Hash128.Compute(Application.dataPath) + "." + guid;
            _gridHeight = EditorPrefs.GetFloat(_viewKey + ".height", SessionState.GetFloat("XlsxInspector.GridHeight", SpreadsheetSettings.instance.PanelHeight));
            string csv = EditorPrefs.GetString(_viewKey + ".csv", SessionState.GetString(_layoutKey, ""));
            _csvLayout = DeserializeCsvLayout(csv);
            string view = EditorPrefs.GetString(_viewKey + ".view", "");
            _viewLayout = view.Length == 0 ? new ViewLayout() : JsonUtility.FromJson<ViewLayout>(view);
        }

        private void StoreViewLayout()
        {
            var sheet = _viewLayout.sheets.Find(item => item.name == _document.SheetNames[_document.SheetIndex]);
            if (sheet == null)
            {
                sheet = new SheetView { name = _document.SheetNames[_document.SheetIndex] };
                _viewLayout.sheets.Add(sheet);
            }
            sheet.scroll = _scroll;
            _viewLayout.sheet = _document.SheetIndex;
            EditorPrefs.SetString(_viewKey + ".view", JsonUtility.ToJson(_viewLayout));
            EditorPrefs.SetFloat(_viewKey + ".height", _gridHeight);
        }

        private void RestoreSheetView()
        {
            var sheet = _viewLayout.sheets.Find(item => item.name == _document.SheetNames[_document.SheetIndex]);
            _scroll = sheet == null ? Vector2.zero : sheet.scroll;
        }

        private void StoreCsvLayout()
        {
            if (_document.IsCsv)
            {
                EditorPrefs.SetString(_viewKey + ".csv", JsonUtility.ToJson(_csvLayout));
            }
        }
    }
}
