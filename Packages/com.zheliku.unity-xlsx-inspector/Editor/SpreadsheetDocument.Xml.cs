using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace XlsxInspector
{
    internal sealed partial class SpreadsheetDocument
    {
        // x:sheetData 与 sheetData 在 OOXML 中等价，但部分表格 Diff 工具只识别默认命名空间。
        // 在最终写出边界统一主命名空间，不改关系、公式、值或扩展命名空间。
        private void NormalizeSpreadsheetNamespaces()
        {
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            _workbookStream.Position = 0;
            using (var archive = new ZipArchive(_workbookStream, ZipArchiveMode.Update, true))
            {
                foreach (var entry in archive.Entries.Where(item => item.FullName.EndsWith(".xml")).ToList())
                {
                    XDocument xml;
                    using (var input = entry.Open())
                    {
                        xml = XDocument.Load(input, LoadOptions.PreserveWhitespace);
                    }
                    if (xml.Root.Name.Namespace != main || xml.Root.GetDefaultNamespace() == main)
                    {
                        continue;
                    }
                    // 保留原来的前缀绑定，扩展属性里的 QName 仍可引用它；元素优先使用新的默认绑定。
                    xml.Root.SetAttributeValue("xmlns", main.NamespaceName);
                    string path = entry.FullName;
                    entry.Delete();
                    using var output = archive.CreateEntry(path, CompressionLevel.Optimal).Open();
                    xml.Save(output, SaveOptions.DisableFormatting);
                }
            }
            _workbookStream.Position = 0;
        }
    }
}
