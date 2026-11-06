# Changelog

## [Unreleased]

## [1.1.1] - 2026-11-07

### Added

- Toolbar Find button to reopen and focus search without a keyboard shortcut.

### Changed

- Show Width / Height by default; the row can be closed and reopened from Format or the cell context menu.
- Use enclosed native buttons, subtle search-option colors and standard-height search fields.
- Translate the English README from the revised Chinese guide and refresh Inspector screenshots.

### Fixed

- Allocate layout regions and register grid scroll controls before processing input, keeping worksheet tabs and scrollbars in place while buttons are held.
- Use a native horizontal scrollbar for worksheet tabs without a second ScrollView state.

## [1.1.0] - 2026-11-06

### Added

- Find/Replace bar with case-sensitive, whole-word and .NET Regex matching, matching-cell counts, previous/next navigation and selection scope.
- Replace one or all matching cells, including regex capture-group replacement and a single Undo step for Replace All.
- Ctrl/Cmd+F, Ctrl/Cmd+H, Enter/Shift+Enter, F3/Shift+F3 and Escape shortcuts for search.
- Source patch and build script for the MIT-only ClosedXML variant.

### Changed

- Remove SixLabors.Fonts, embedded CarlitoBare fonts and the now-unused Unsafe assembly; keep workbook styles and images, using Unity for text measurements.
- Remove the unused Documentation~ directory and the File behavior/Development README sections.
- Synchronize numbered English and Chinese documentation and update search screenshots.

### Fixed

- Keep worksheet tab browsing and grid scroll positions unchanged while pressing search options or expanding Replace.

## [1.0.0] - 2026-11-06

Initial release: edit configuration tables in the Unity Inspector.

### Added

- Persist personal CSV formatting, panel height, worksheet selection, and scroll positions across Editor restarts.
- Extend empty grid space while scrolling, dragging a selection, or using keyboard navigation, without adding records or CSV layout entries.
- Add Project Settings for layout defaults, grid font size, alternating row colors, color schemes, tooltips, shortcut hints, and selection scrolling.
- Include Forest, Ocean, Plum and Graphite palettes for Light/Dark themes, plus separate custom palettes for each theme.
- Use a coordinated light/dark palette, selected headers, an active-cell outline, and an accent marker on the active worksheet tab.
- Add hover help to toolbar actions, cells, headers, resize handles, and settings.
- Highlight the actual content range, expand it on input, and shrink it when edge contents are cleared. Dimmed cells remain editable and accept paste.
- Publish as Unity Xlsx Inspector with the package ID `com.zheliku.unity-xlsx-inspector`.
- Detect pipe-delimited CSV files alongside comma, semicolon, and tab delimiters; preserve padding, quoting, and record newlines when saving.
- Validate the EditMode suite on Unity 2022.3.62f3 and Unity 6000.0.70f1 on Windows.
- Use English workbook examples and screenshots, with concise English and Chinese READMEs.
- Preview, edit, and save `.xlsx` and `.csv` files while keeping Unity's original icons and importers.
- Bottom worksheet tabs, inline editing, an edit bar, and Enter/Tab/Escape navigation.
- Range selection, row/column selection, select-all, text copy/cut/paste, clear, and search.
- Automatic scrolling at all four grid edges while dragging a selection, including when the pointer stays still.
- Single and bulk row/column insertion and deletion, with fixed header + buttons to insert below or to the right of the selection.
- Row and column resizing, range-wide dimensions, auto-fit, alignment, wrapping, and a resizable Inspector panel.
- Unity Undo/Redo across asset switches and saves, local drafts, and detection of external file changes.
- Editor-only dependencies, complete third-party licenses, and EditMode regression tests.
- Separate English and Chinese root READMEs with screenshots and usage instructions, plus Chinese code and release guides.

### Fixed

- Keep worksheet scroll controls stable while resizing the grid by allocating their controls before the visible row and column headers.
- Reclaim offscreen empty preview rows and columns after scrolling back and releasing the mouse; preserve content, selection coordinates, and saved dimensions.
- Keep worksheet tabs inside a bounded horizontal viewport, with wheel/scrollbar browsing, previous/next navigation, an all-sheets menu, and automatic reveal of the active tab. Hide the worksheet bar for CSV.
- Use native UI Toolkit settings fields so hover help covers both labels and input areas.
- Select the actual content range with Ctrl/Cmd+A, excluding cleared or appended empty trailing cells.
- Compare sampled CSV record widths instead of only counting punctuation in the header.
- Write SpreadsheetML with a default namespace for compatibility with XLSX Diff readers that only recognize unprefixed tags.
- Keep workbook streams alive for repeated snapshots and saves; support workbooks whose Normal style omits its built-in identifier.
- Recover drafts affected by duplicate invalid Normal styles and retain appended empty rows/columns after saving and reopening.
- Keep panel resizing responsive when dragging upwards across cells, and give new inline editors focus after their controls are registered.
- Extend the grid before navigating away from its last reserved row or column.
- Detect CSV record newlines independently of line breaks inside quoted fields.
