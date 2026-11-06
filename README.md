# Unity Xlsx Inspector

Edit `.xlsx` and `.csv` files directly in the Unity Inspector. Select a file in the Project window, edit its cells in the Inspector, then save back to the original file.

English | [简体中文](README.zh-CN.md)

![XLSX editing in the Inspector](image/README.zh-CN/1793945502651.png)

## 1. Requirements

- Unity **2022.3 LTS or newer**, including Unity 6.
- **.NET Standard 2.1** API compatibility.
- Editor-only UPM package. Dependencies are bundled.

| Tested Editor | Platform | Validation |
| --- | --- | --- |
| 2022.3.62f3 | Windows | EditMode tests |
| 6000.0.70f1 | Windows | EditMode tests |

## 2. Installation

Open **Window → Package Manager → + → Install package from git URL** and paste:

```text
https://github.com/zheliku/UnityXlsxInspector.git?path=/Packages/com.zheliku.unity-xlsx-inspector
```

## 3. Features

### 3.1 Cell editing and clipboard

Double-click a cell or press **F2** to edit it. **Enter** commits and moves down; **Tab** moves right; **Esc** cancels the current input. The edit bar supports multiline text.

Select a range by dragging or Shift-clicking. Copy, cut, paste, clear, Undo and Redo work across cells and asset switches. Paste directly into empty cells to extend the table. Click **Save** to write the file; pending edits are kept as local drafts between Editor sessions.

![Inline cell editing](image/cell-editing.png)

### 3.2 Worksheets

XLSX worksheet tabs stay inside the Inspector width. Browse with the mouse wheel or horizontal scrollbar, use **‹ / ›** for the previous/next sheet, or **▾** to choose from the complete list. Switching sheets brings the active tab into view. CSV has no worksheet bar.

![Worksheet navigation](image/worksheet-tabs.png)

### 3.3 Rows, columns and sizing

**Rows / Columns** inserts or deletes one or many rows and columns. The row-header **+** inserts below the selection; the column-header **+** inserts to its right. Right-click a **+** to set the count. Inserting and deleting shift existing cells; **Clear** empties them in place.

![Bulk row insertion](image/batch-insert.png)

Drag header edges to resize rows and columns, or use **Width / Height** for precise values across the selection. The input row is shown by default; close it when you need more space and reopen it from **Format → Column Width and Row Height**. **Auto Fit** measures the contents; double-clicking a header edge fits that row or column. **Format** and the cell context menu also provide horizontal/vertical alignment and text wrapping.

Drag the bar below the grid to adjust its height. The content range stays bright, while empty space is dimmed. The empty preview grows as you scroll or drag a selection; after returning and releasing the mouse, offscreen empty space is reclaimed so scrollbar thumbs grow again. Filled cells stay in range.

![Range sizing, alignment and wrapping](image/range-formatting.png)

### 3.4 CSV and search

CSV detects **comma, semicolon, Tab and pipe (`|`)** delimiters. Saving preserves the delimiter, encoding, record line endings and field spaces. Supported encodings are UTF-8 with or without BOM, and UTF-16/32 with BOM. Quoted fields can contain delimiters, quotes and line breaks.

Click **Find** in the toolbar or press **Ctrl/Cmd+F** to open and focus search. **Ctrl/Cmd+H** opens Replace. The Find/Replace bar supports **Aa** (Match Case), **ab** (Whole Word), `.*` (Regex), and selection scope. Counts show matching cells; **↑ / ↓** navigate them. Expand **›** to replace the current matching cell or all results. Regex uses .NET syntax, including `$1` capture groups in replacements.

![Pipe-delimited CSV search](image/search.png)

### 3.5 Layout and colors

Open **Edit → Project Settings → Unity Xlsx Inspector**, or click **Settings** in the Inspector toolbar.

| Group | Options |
| --- | --- |
| Layout | Default CSV sizes, panel height, initial preview rows and columns |
| Appearance | Grid font size, alternating rows, color scheme, cell tooltips, shortcut hints |
| Selection scrolling | Speed and edge distance |

**Forest, Ocean, Plum and Graphite** follow Unity's Light/Dark theme. **Custom** provides separate palettes for each theme: backgrounds, headers, grid lines, text, accent and search colors. Hover over a setting for its description.

![Project settings](image/settings.png)

CSV display formatting, panel height, active worksheet and scroll positions are remembered on this computer per project and asset. Shared defaults live in `ProjectSettings/UnityXlsxInspector.asset`.

![Dark theme](image/dark-theme.png)

## 4. Shortcuts

| Shortcut | Action |
| --- | --- |
| F2 / Enter | Edit the active cell |
| Enter / Shift+Enter | Commit and move down / up |
| Tab / Shift+Tab | Commit and move right / left |
| Esc | Cancel the current cell input |
| Shift+click / Shift+arrow | Extend the selection |
| Ctrl+A / C / X / V | Select the content range / copy / cut / paste |
| Delete / Backspace | Clear selected contents |
| Ctrl+F / Ctrl+H | Find / replace |
| Enter / Shift+Enter in Find | Next / previous match |
| F3 / Shift+F3 | Next / previous match |
| Ctrl+S / Z | Save / undo |
| Ctrl+Y / Ctrl+Shift+Z | Redo |

On macOS, you can use **Cmd** instead of Ctrl.

## 5. License

Project code and all **seven bundled library assemblies** use **MIT**. Dependency versions and original copyright notices are in [Third Party Notices](Packages/com.zheliku.unity-xlsx-inspector/Third%20Party%20Notices.md).
