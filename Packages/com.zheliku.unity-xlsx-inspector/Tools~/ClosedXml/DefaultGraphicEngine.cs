using System;
using System.IO;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace ClosedXML.Graphics
{
    /// <summary>
    /// 本包专用的无字体后端：保留上游图片信息读取和列宽换算，文字测量由 Unity Inspector 负责。
    /// 此源码替换 ClosedXML 上游同名文件，不包含字体解析代码或字体文件。
    /// </summary>
    public sealed class DefaultGraphicEngine : IXLGraphicEngine
    {
        private readonly ImageInfoReader[] _imageReaders =
        {
            new PngInfoReader(), new JpegInfoReader(), new GifInfoReader(),
            new TiffInfoReader(), new BmpInfoReader(), new EmfInfoReader(),
            new WmfInfoReader(), new WebpInfoReader(), new PcxInfoReader()
        }; // 复用 ClosedXML 的 MIT 图片格式读取器，保持含图片的工作簿读写。

        /// <summary>供 ClosedXML 工作簿默认构造流程使用的共享实例。</summary>
        public static Lazy<DefaultGraphicEngine> Instance { get; } = new Lazy<DefaultGraphicEngine>(() => new DefaultGraphicEngine(""));

        /// <summary>创建不加载字体文件的后端，参数保留上游构造器的签名。</summary>
        /// <param name="fallbackFont">此构建不加载字体，参数不参与任何字体选择。</param>
        public DefaultGraphicEngine(string fallbackFont)
        {
        }

        /// <summary>通过上游读取器获取工作簿图片的格式、尺寸和分辨率。</summary>
        /// <param name="stream">位于图片起点的输入流。</param>
        /// <param name="expectedFormat">调用方提供的格式提示；实际格式由读取器识别。</param>
        /// <returns>图片格式与尺寸信息。</returns>
        /// <exception cref="ArgumentException">图片格式无法识别。</exception>
        public XLPictureInfo GetPictureInfo(Stream stream, XLPictureFormat expectedFormat)
        {
            foreach (var reader in _imageReaders)
            {
                if (reader.TryGetInfo(stream, out var info))
                {
                    return info;
                }
            }
            throw new ArgumentException("Unable to determine the format of the image.");
        }

        /// <summary>使用 Inspector 原有的 7 像素列宽基准，避免加载工作簿时依赖操作系统字体。</summary>
        /// <param name="font">工作簿字体的字号；字体名称和样式原样保留在工作簿中。</param>
        /// <param name="dpiX">水平方向的每英寸像素数。</param>
        /// <returns>按字号与 DPI 缩放后的数字宽度基准。</returns>
        public double GetMaxDigitWidth(IXLFontBase font, double dpiX)
        {
            return 7 * font.FontSize / 11 * dpiX / 96;
        }

        // 本包不调用 ClosedXML 的字体 Auto Fit；明确阻止它被误当成精确字体测量器。
        /// <inheritdoc />
        public double GetTextHeight(IXLFontBase font, double dpiY)
        {
            throw new NotSupportedException("Use Unity Xlsx Inspector Auto Fit for text measurements.");
        }

        /// <inheritdoc />
        public double GetTextWidth(string text, IXLFontBase font, double dpiX)
        {
            throw new NotSupportedException("Use Unity Xlsx Inspector Auto Fit for text measurements.");
        }

        /// <inheritdoc />
        public double GetDescent(IXLFontBase font, double dpiY)
        {
            throw new NotSupportedException("Use Unity Xlsx Inspector Auto Fit for text measurements.");
        }

        /// <inheritdoc />
        public GlyphBox GetGlyphBox(ReadOnlySpan<int> graphemeCluster, IXLFontBase font, Dpi dpi)
        {
            throw new NotSupportedException("Use Unity Xlsx Inspector Auto Fit for text measurements.");
        }
    }
}
