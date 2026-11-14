# Unity Xlsx Inspector

View, edit, and save `.xlsx` and `.csv` files in the Unity Inspector. Includes worksheet tabs, range copy/paste, bulk row and column edits, resizing, formatting, and Undo/Redo.

[English documentation](https://github.com/zheliku/UnityXlsxInspector#readme) | [简体中文](https://github.com/zheliku/UnityXlsxInspector/blob/main/README.zh-CN.md)

## 1. Install

Choose **Install package from git URL** in Package Manager and enter:

```text
https://github.com/zheliku/UnityXlsxInspector.git?path=/Packages/com.zheliku.unity-xlsx-inspector#v1.2.0
```

Requires Unity 2022.3+ with .NET Standard 2.1. Tested on Windows with Unity 2022.3.62f3 and 6000.0.70f1. Excel libraries are bundled and Editor-only. XLSX files import as `XlsxInspector.XlsxAsset`, which can be referenced by runtime scripts and exposes the original workbook data through `Bytes`. CSV remains `TextAsset`.

The project README contains the full guide, screenshots, and shortcuts in separate English and Chinese versions.

Release tarballs are available on [GitHub Releases](https://github.com/zheliku/UnityXlsxInspector/releases). Download the `.tgz` file and choose **Install package from tarball** in Unity Package Manager.

## 2. License

Version **1.2.0**. Project code and the seven bundled library assemblies use MIT. Original license texts and third-party notices are included in `Third Party Notices.md` and `ThirdPartyNotices~`.
