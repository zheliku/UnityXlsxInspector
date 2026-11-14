using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace XlsxInspector
{
    /// <summary>将 .xlsx 原文件导入为可引用的 <see cref="XlsxAsset"/>，不解析或改写工作簿。</summary>
    [ScriptedImporter(1, "xlsx")]
    public sealed class XlsxImporter : ScriptedImporter
    {
        /// <summary>读取源文件字节，并将工作簿资源注册为此文件的主资源。</summary>
        /// <param name="context">Unity 提供的导入上下文，包含源文件路径及导入产物的注册接口。</param>
        public override void OnImportAsset(AssetImportContext context)
        {
            // 固定标识符用于在重复导入时保持资源引用稳定。
            var asset = ScriptableObject.CreateInstance<XlsxAsset>();
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
            asset.Bytes = File.ReadAllBytes(FileUtil.GetPhysicalPath(context.assetPath));
            // GUID 随图标的 .meta 一起分发，安装到 Packages 或 Assets、移动目录后都能定位。
            string iconPath = AssetDatabase.GUIDToAssetPath("21f5a65f8bb928d458bcab9e4f2fcd29");
            context.DependsOnSourceAsset(iconPath);
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            EditorGUIUtility.SetIconForObject(asset, icon);
            context.AddObjectToAsset("workbook", asset, icon);
            context.SetMainObject(asset);
        }
    }
}
