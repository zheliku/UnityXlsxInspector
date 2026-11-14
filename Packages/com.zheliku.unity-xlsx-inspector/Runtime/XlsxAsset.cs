using UnityEngine;

namespace XlsxInspector
{
    /// <summary>
    /// Unity 导入的 Excel 工作簿资源，可用于脚本字段、场景和预制体的强类型引用。
    /// </summary>
    /// <remarks>
    /// 资源保存原始 XLSX 字节；表格预览和编辑由 Editor 程序集提供。
    /// 运行时可将 <see cref="Bytes"/> 交给项目使用的 Excel 读取库。
    /// </remarks>
    public sealed class XlsxAsset : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private byte[] _bytes; // 导入时读取的原始 XLSX 内容，随资源一起序列化。

        /// <summary>获取当前导入版本的 XLSX 原始文件内容。</summary>
        /// <value>工作簿的二进制数据，可用于创建内存流并读取 Excel 内容。</value>
        /// <remarks>重新导入源文件时更新此数据；运行时读取不需要 UnityEditor。</remarks>
        public byte[] Bytes
        {
            get => _bytes;
            internal set => _bytes = value;
        }
    }
}
