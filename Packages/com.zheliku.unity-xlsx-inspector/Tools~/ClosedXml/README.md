# ClosedXML build

Based on the MIT-licensed [ClosedXML 0.105.1 source](https://github.com/ClosedXML/ClosedXML/tree/0.105.1).

`build.ps1` downloads that tag, removes the SixLabors.Fonts package reference and embedded font resources, replaces `Graphics/DefaultGraphicEngine.cs`, and builds the bundled `Editor/Plugins/ClosedXML.dll` for .NET Standard 2.1. Requires the .NET SDK and PowerShell 7.

```powershell
./build.ps1
```

The replacement retains ClosedXML's image readers and uses a seven-pixel digit-width baseline at 11 pt / 96 DPI. Workbook font names and styles are preserved. Text measurement and Auto Fit are handled by Unity Xlsx Inspector; ClosedXML's font measurement methods are unsupported in this build.

The original MIT copyright and license are preserved in `ThirdPartyNotices~/ClosedXML.txt`.
