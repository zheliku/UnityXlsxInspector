# Third Party Notices

This package includes the following MIT-licensed assemblies for use in the Unity Editor. Versions below are upstream package versions; assembly versions may differ. Full license texts are distributed in `ThirdPartyNotices~`.

| Package | Version | License | Included license |
| --- | --- | --- | --- |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML/tree/0.105.1) | 0.105.1, custom build | MIT | [ClosedXML.txt](ThirdPartyNotices~/ClosedXML.txt) |
| [ClosedXML.Parser](https://github.com/ClosedXML/ClosedXML.Parser) | 2.0.0 | MIT | [ClosedXML.Parser.txt](ThirdPartyNotices~/ClosedXML.Parser.txt) |
| [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | 3.1.1 | MIT | [OpenXML.txt](ThirdPartyNotices~/OpenXML.txt) |
| [DocumentFormat.OpenXml.Framework](https://github.com/dotnet/Open-XML-SDK) | 3.1.1 | MIT | [OpenXML.txt](ThirdPartyNotices~/OpenXML.txt) |
| [ExcelNumberFormat](https://github.com/andersnm/ExcelNumberFormat) | 1.1.0 | MIT | [ExcelNumberFormat.txt](ThirdPartyNotices~/ExcelNumberFormat.txt) |
| [RBush.Signed](https://www.nuget.org/packages/RBush.Signed/4.0.0) | 4.0.0 | MIT | [RBush.txt](ThirdPartyNotices~/RBush.txt) |
| [System.IO.Packaging](https://www.nuget.org/packages/System.IO.Packaging/8.0.1) | 8.0.1 | MIT | [System.IO.Packaging.txt](ThirdPartyNotices~/System.IO.Packaging.txt), [upstream third-party notices](ThirdPartyNotices~/System.IO.Packaging-ThirdParty.txt) |

The ClosedXML build removes the SixLabors.Fonts dependency and all embedded CarlitoBare font resources. It uses the included image readers and a fixed digit-width baseline; this package performs text measurements with Unity. The replacement source and build script are in [Tools~/ClosedXml](Tools~/ClosedXml). Upstream workbook parsing, editing, formulas and image readers are retained.

Unity Xlsx Inspector's own source code is licensed under [MIT](LICENSE.md). That license does not replace the licenses listed above.
