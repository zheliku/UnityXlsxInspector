using UnityEngine;

namespace XlsxInspector
{
    /// <summary>
    /// Unity Undo 系统使用的序列化快照。对象隐藏在 Editor 会话中，不会生成项目资产或进入游戏构建。
    /// </summary>
    /// <remarks>
    /// ScriptableObject 单独放在同名文件中，让 Unity 能稳定识别其脚本类型和序列化字段。
    /// 不在这里执行文件读写，原文件、草稿及 Undo 恢复由 Inspector 对应 partial 文件协调。
    /// </remarks>
    internal sealed class SpreadsheetHistoryState : ScriptableObject
    {
        [SerializeField] internal string AssetGuid; // 资源标识，切换资源后可以找到原来的历史对象。
        [SerializeField] internal string Data; // 可重新打开的 Excel / CSV 文件快照。
        [SerializeField] internal string Layout; // CSV 的尺寸和对齐快照。
        [SerializeField] internal int Sheet; // 操作发生时所在的 Sheet。
        [SerializeField] internal int Revision; // 数据版本，单纯修改 CSV 显示格式时不变。
        [SerializeField] internal int Step; // 每个可撤销操作都有独立步骤，包含 CSV 的显示修改。
        [SerializeField] internal Vector2Int Selected; // 操作发生时的活动单元格。
        [SerializeField] internal Vector2Int Anchor; // 操作发生时的选区起点。
    }
}
