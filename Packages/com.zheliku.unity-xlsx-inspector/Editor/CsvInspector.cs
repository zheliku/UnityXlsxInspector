using UnityEditor;
using UnityEngine;

namespace XlsxInspector
{
    /// <summary>
    /// 为 Unity 的 TextAsset 注册 CSV Inspector；界面和文件操作全部复用 SpreadsheetInspector。
    /// 非 CSV 的文本资源仍由基类转发给 Unity 原生文本 Inspector。
    /// </summary>
    [CustomEditor(typeof(TextAsset))]
    [CanEditMultipleObjects]
    public sealed class CsvInspector : SpreadsheetInspector
    {
    }
}
