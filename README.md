# Unity Xlsx Inspector

English | [简体中文](README.zh-CN.md)

Edit `.xlsx` and `.csv` files directly in the Unity Inspector. Select a file in Project, edit its cells, and save it back to the original file. No separate window or Excel installation is needed. Asset icons and importers are kept.

![XLSX editing in the Inspector](image/README.zh-CN/1793945502651.png)

## Requirements

- Unity **2022.3 LTS or newer**, including Unity 6.
- **.NET Standard 2.1** API compatibility.
- Editor-only UPM package. Dependencies are bundled.

| Tested Editor | Platform | Validation |
| --- | --- | --- |
| 2022.3.62f3 | Windows | EditMode tests |
| 6000.0.70f1 | Windows | EditMode tests |

## Installation

Open **Window → Package Manager → + → Install package from git URL** and paste:

```text
https://github.com/zheliku/UnityXlsxInspector.git?path=/Packages/com.zheliku.unity-xlsx-inspector
```

You can also copy `Packages/com.zheliku.unity-xlsx-inspector` into your project's `Packages` folder.

## Features

### Cell editing and clipboard

Double-click a cell or press **F2** to edit it. **Enter** commits and moves down; **Tab** moves right; **Esc** cancels the current input. The edit bar supports multiline text.

Select a range by dragging or Shift-clicking. Copy, cut, paste, clear, Undo and Redo work across cells and asset switches. Paste directly into empty cells to extend the table. Click **Save** to write the file; pending edits are kept as local drafts between Editor sessions.

![Inline cell editing](image/cell-editing.png)

### Worksheets

XLSX worksheet tabs stay inside the Inspector width. Browse with the mouse wheel or horizontal scrollbar, use **‹ / ›** for the previous/next sheet, or **▾** to choose from the complete list. Switching sheets brings the active tab into view. CSV has no worksheet bar.

![Worksheet navigation](image/worksheet-tabs.png)

### Rows, columns and sizing

**Rows / Columns** inserts or deletes one or many rows and columns. The row-header **+** inserts below the selection; the column-header **+** inserts to its right. Right-click a **+** to set the count. Inserting and deleting shift existing cells; **Clear** empties them in place.

![Bulk row insertion](image/batch-insert.png)

Drag header edges to resize rows and columns, or enter **Width / Height** for the whole selection. **Auto Fit** measures the contents; double-clicking a header edge fits that row or column. **Format** and the cell context menu provide horizontal/vertical alignment and text wrapping.

Drag the bar below the grid to adjust its height. The content range stays bright, while empty space is dimmed. The empty preview grows as you scroll or drag a selection; after returning and releasing the mouse, offscreen empty space is reclaimed so scrollbar thumbs grow again. Filled cells stay in range.

![Range sizing, alignment and wrapping](image/range-formatting.png)

### CSV and search

CSV detects **comma, semicolon, Tab and pipe (`|`)** delimiters. Saving preserves the delimiter, encoding, record line endings and field spaces. Supported encodings are UTF-8 with or without BOM, and UTF-16/32 with BOM. Quoted fields can contain delimiters, quotes and line breaks.

Search ignores case. Matching cells get a marker; **Find Next** moves through the current worksheet.

![Pipe-delimited CSV search](image/search.png)

### Layout and colors

Open **Edit → Project Settings → Unity Xlsx Inspector**, or click **Settings** in the Inspector toolbar.

| Group | Options |
| --- | --- |
| Layout | Default CSV sizes, panel height, initial preview rows and columns |
| Appearance | Grid font size, alternating rows, color scheme, cell tooltips, shortcut hints |
| Selection scrolling | Speed and edge distance |

**Forest, Ocean, Plum and Graphite** follow Unity's Light/Dark theme. **Custom** provides separate palettes for each theme: backgrounds, headers, grid lines, text, accent and search colors. Hover over a setting for its description.

![Project settings](image/settings.png)

CSV display formatting, panel height, active worksheet and scroll positions are remembered on this computer per project and asset. Shared defaults live in `ProjectSettings/UnityXlsxInspector.asset`. XLSX dimensions and alignment are saved in the workbook. Grid font size and colors only affect the Inspector.

![Dark theme](image/dark-theme.png)

## Shortcuts

| Shortcut | Action |
| --- | --- |
| F2 / Enter | Edit the active cell |
| Enter / Shift+Enter | Commit and move down / up |
| Tab / Shift+Tab | Commit and move right / left |
| Esc | Cancel the current cell input |
| Shift+click / Shift+arrow | Extend the selection |
| Ctrl+A / C / X / V | Select the content range / copy / cut / paste |
| Delete / Backspace | Clear selected contents |
| Ctrl+S / Z | Save / undo |
| Ctrl+Y / Ctrl+Shift+Z | Redo |

Use **Cmd** instead of Ctrl on macOS. When a text field has focus, clipboard shortcuts act on its text.

## File behavior

- XLSX keeps numbers and booleans as their types; `001` stays text. Start with `'` to force text or `=` for a formula. Formula previews use cached results; Excel/WPS recalculates them when opened.
- The grid displays cell contents, basic alignment, wrapping and dimensions. Merged layouts, workbook fonts, charts and conditional formatting are not rendered. Workbook round-tripping uses ClosedXML.
- Drafts are stored in `Library/XlsxInspector`; Undo history lasts for the current Editor session. **Reload** asks before discarding edits. Saving checks for external file changes and keeps pending edits if a conflict or file lock prevents writing.
- Workbooks and Undo snapshots are loaded in memory. This package is intended for configuration tables. Cached package assets are read-only.

CSV detection samples the first 20 logical records. Ambiguous files may need their delimiter made consistent. [RFC 4180](https://www.rfc-editor.org/rfc/rfc4180) describes comma-separated CSV; semicolon, Tab and pipe are supported dialects, as described by [CSVW](https://www.w3.org/TR/tabular-metadata/#dialect-descriptions).

## Development

Source is in `Packages/com.zheliku.unity-xlsx-inspector`. Install Unity Test Framework, add `"testables": ["com.zheliku.unity-xlsx-inspector"]` to the project manifest, then run **Test Runner → EditMode → XlsxInspector.Tests**. UI tests need a graphics-enabled Editor and a system clipboard.

[Code guide](Packages/com.zheliku.unity-xlsx-inspector/Documentation~/code-guide.md) · [Changelog](Packages/com.zheliku.unity-xlsx-inspector/CHANGELOG.md) · [Release guide](Packages/com.zheliku.unity-xlsx-inspector/Documentation~/releasing.md)

## License

Project code is [MIT](LICENSE.md). Most bundled libraries are MIT, including ClosedXML, Open XML SDK, ExcelNumberFormat, RBush and the two System assemblies. **SixLabors.Fonts 1.0.0 uses Apache-2.0**; ClosedXML's embedded **CarlitoBare font data uses SIL OFL 1.1**. Their license texts and notices are included in the package.

See [Third Party Notices](Packages/com.zheliku.unity-xlsx-inspector/Third%20Party%20Notices.md) for dependency versions, upstream links and full licenses.
