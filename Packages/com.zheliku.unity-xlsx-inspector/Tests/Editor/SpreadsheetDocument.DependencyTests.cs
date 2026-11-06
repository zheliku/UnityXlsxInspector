using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using NUnit.Framework;

namespace XlsxInspector.Tests
{
    public partial class SpreadsheetDocumentTests
    {
        /// <summary>验证最终分发的程序集不再引用字体库，也不携带字体资源。</summary>
        [Test]
        public void ClosedXmlBuildContainsNoFontDependenciesOrFontResources()
        {
            var assembly = typeof(XLWorkbook).Assembly;
            Assert.That(assembly.GetReferencedAssemblies().Any(item => item.Name.StartsWith("SixLabors", StringComparison.Ordinal)), Is.False);
            Assert.That(assembly.GetManifestResourceNames().Any(item => item.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(assembly.Location), "SixLabors.Fonts.dll")), Is.False);
        }

        /// <summary>无字体版本仍保留图片、字体样式、其他 Sheet 和图片数据，不依赖字体文件。</summary>
        [Test]
        public void FontlessBuildPreservesPicturesAndWorkbookFontStyles()
        {
            string path = Path.Combine(_directory, "picture.xlsx");
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
            using (var workbook = new XLWorkbook())
            using (var image = new MemoryStream(png))
            {
                var sheet = workbook.AddWorksheet("Pictures");
                sheet.Cell("A1").Value = "original";
                sheet.Cell("A1").Style.Font.FontName = "Uninstalled Custom Font";
                sheet.Cell("A1").Style.Font.Bold = true;
                sheet.AddPicture(image, "Logo").MoveTo(sheet.Cell("B2"));
                workbook.AddWorksheet("Other").Cell("A1").Value = "kept";
                workbook.SaveAs(path);
            }
            using (var document = new SpreadsheetDocument(path))
            {
                document.SetText(0, 0, "updated");
                document.Save();
            }
            using var reopened = new XLWorkbook(path);
            var changed = reopened.Worksheet("Pictures");
            Assert.That(changed.Cell("A1").Style.Font.FontName, Is.EqualTo("Uninstalled Custom Font"));
            Assert.That(changed.Cell("A1").Style.Font.Bold, Is.True);
            Assert.That(changed.Pictures.Single().ImageStream.ToArray(), Is.EqualTo(png));
            Assert.That(reopened.Worksheet("Other").Cell("A1").GetString(), Is.EqualTo("kept"));
        }
    }
}
