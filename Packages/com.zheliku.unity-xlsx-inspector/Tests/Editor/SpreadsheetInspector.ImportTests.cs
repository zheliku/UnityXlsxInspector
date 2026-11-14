using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace XlsxInspector.Tests
{
    public partial class SpreadsheetInspectorTests
    {
        /// <summary>从 Inspector 保存后更新导入字节，重复导入与移动文件保持 GUID 和本地资源 ID。</summary>
        [Test]
        public void XlsxImportKeepsItsIdentityAcrossSaveReimportAndMove()
        {
            Open(_xlsxPath);
            var asset = (XlsxAsset)_editor.target;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId), Is.True);
            Assert.That(asset.Bytes, Is.EqualTo(File.ReadAllBytes(_xlsxPath)));

            // 复用真实的保存入口，同时检查导入后的字节与现有的文档编辑状态。
            SetField("_editBuffer", "saved from Inspector");
            SetField("_editChanged", true);
            Invoke("Save");
            Assert.That(GetField<string>("_error"), Is.Null);
            Assert.That(Document.GetText(0, 0), Is.EqualTo("saved from Inspector"));
            Assert.That(Document.IsDirty, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<XlsxAsset>(_xlsxPath).Bytes, Is.EqualTo(File.ReadAllBytes(_xlsxPath)));

            AssetDatabase.ImportAsset(_xlsxPath, ImportAssetOptions.ForceUpdate);
            string movedPath = _folder + "/renamed.xlsx";
            Assert.That(AssetDatabase.MoveAsset(_xlsxPath, movedPath), Is.Empty);
            var moved = AssetDatabase.LoadAssetAtPath<XlsxAsset>(movedPath);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(moved, out string movedGuid, out long movedId), Is.True);
            Assert.That(movedGuid, Is.EqualTo(guid));
            Assert.That(movedId, Is.EqualTo(localId));
            Assert.That(moved.Bytes, Is.EqualTo(File.ReadAllBytes(movedPath)));
        }

        /// <summary>专用 XLSX Inspector 不再接管其他 DefaultAsset 资源。</summary>
        [Test]
        public void NonSpreadsheetFilesKeepTheirNativeInspector()
        {
            string path = _folder + "/unrelated.bin";
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            AssetDatabase.ImportAsset(path);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            var editor = Editor.CreateEditor(asset);
            try
            {
                Assert.That(asset, Is.TypeOf<DefaultAsset>());
                Assert.That(editor.GetType().FullName, Is.EqualTo("UnityEditor.DefaultAssetInspector"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        /// <summary>XlsxAsset 可被运行时程序集引用，不依赖 UnityEditor 或编辑器的 Excel 库。</summary>
        [Test]
        public void XlsxAssetRuntimeAssemblyDoesNotReferenceEditorLibraries()
        {
            string[] references = typeof(XlsxAsset).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
            Assert.That(typeof(XlsxAsset).Assembly.GetName().Name, Is.EqualTo("XlsxInspector.Runtime"));
            Assert.That(references.Any(name => name.StartsWith("UnityEditor", StringComparison.Ordinal)
                || name.StartsWith("ClosedXML", StringComparison.Ordinal) || name == "XlsxInspector.Editor"), Is.False);
        }

        /// <summary>重复导入后，Project 图标与资源对象图标都保留为文件图标。</summary>
        [Test]
        public void XlsxFileIconIsAvailableForBothProjectAndInspector()
        {
            AssetDatabase.ImportAsset(_xlsxPath, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<XlsxAsset>(_xlsxPath);
            Texture2D objectIcon = EditorGUIUtility.GetIconForObject(asset);
            Texture projectIcon = AssetDatabase.GetCachedIcon(_xlsxPath);
            Assert.That(objectIcon, Is.Not.Null);
            Assert.That(projectIcon, Is.Not.Null);
            Assert.That(AssetPreview.GetMiniThumbnail(asset), Is.SameAs(objectIcon));
            Assert.That(objectIcon.name, Is.Not.EqualTo("ScriptableObject Icon"));
            Assert.That(projectIcon.width, Is.EqualTo(objectIcon.width));
            Assert.That(projectIcon.height, Is.EqualTo(objectIcon.height));
        }
    }
}
