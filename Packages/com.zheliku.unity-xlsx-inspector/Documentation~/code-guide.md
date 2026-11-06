# 给 Unity Editor 初学者的代码导读

## 建议阅读顺序

1. `Editor/CsvInspector.cs` 只有一个注册入口：`CustomEditor(typeof(TextAsset))` 告诉 Unity，选中 TextAsset 时创建我们的 Inspector。
2. `Editor/SpreadsheetInspector.cs` 的 `OnEnable` 判断扩展名。非表格资源转交 Unity 自带 Inspector，因此普通文本和文件夹的原有显示不会被替换。
3. `OnInspectorGUI → DrawSpreadsheet` 是界面的入口，界面顺序是工具栏、编辑栏、操作、尺寸、搜索、网格、调整高度横条和底部 Sheet 标签。
4. `Editor/SpreadsheetDocument.cs` 负责数据。UI 不处理 Excel XML，而是调用 `GetText`、`SetText`、行列操作和 `Save`。
5. `Editor/Csv.cs` 是文件与剪贴板共用的分隔文本读写代码。先看引号内外的状态变化，再看双引号和换行处理。
6. `Tests/Editor/SpreadsheetDocumentTests.cs` 给出最小调用示例，也记录了此前真实出现的数据流和默认样式问题。

## 文件按职责拆分

所有 `SpreadsheetInspector.*.cs` 都是同一个 `partial class` 的不同部分，共用一份字段和 Editor 生命周期。

| 文件 | 阅读重点 |
| --- | --- |
| `SpreadsheetInspector.cs` | 生命周期、资源路径、草稿、保存和错误边界 |
| `SpreadsheetInspector.Toolbar.cs` | 工具栏、输入框和搜索 |
| `SpreadsheetInspector.Sheets.cs` | 固定宽度的 Sheet 标签视口、滚动、前后切换和完整列表 |
| `SpreadsheetInspector.Grid.cs` | 可见区域绘制、固定表头、行列拖动和面板高度 |
| `SpreadsheetInspector.Appearance.cs` | 深浅色皮肤、强调色、表头底色、文字样式和活动格边框 |
| `SpreadsheetInspector.Preview.cs` | 随滚动和键盘导航按需延伸空白预览，不修改文件 |
| `SpreadsheetInspector.Layout.cs` | 当前 Inspector 视口绑定、窗口高度限制和旧布局恢复 |
| `SpreadsheetInspector.Persistence.cs` | CSV 个人布局、面板高度、Sheet 和滚动位置 |
| `SpreadsheetSettings.cs` | Project Settings 中的布局默认值、显示与拖选参数 |
| `SpreadsheetSettings.UI.cs` | 原生 UI Toolkit 设置字段、序列化绑定与整项悬浮提示 |
| `SpreadsheetPalette.cs` | 四套深浅主题预设和自定义颜色数据 |
| `SpreadsheetInspector.Editing.cs` | 编辑缓冲区、选区、快捷键、剪贴板和行列操作 |
| `SpreadsheetInspector.Selection.cs` | 鼠标捕获、跨视口拖选和四边自动滚动 |
| `SpreadsheetInspector.Structure.cs` | 行列菜单、内联批量插入、表头加号和 CSV 格式随行列移动 |
| `SpreadsheetInspector.Formatting.cs` | 对齐、换行、范围尺寸和内容自适应 |
| `SpreadsheetInspector.History.cs` | Unity Undo 桥接、磁盘版本和历史恢复 |
| `SpreadsheetHistoryState.cs` | Unity 可以序列化和撤销的隐藏快照对象 |
| `SpreadsheetDocument.Formatting.cs` | 显示格式与 ClosedXML 格式之间的转换 |
| `SpreadsheetDocument.Structure.cs` | 批量行列操作、数量边界校验、Excel 工作区范围保存 |
| `SpreadsheetDocument.Xml.cs` | 默认 SpreadsheetML 命名空间与表格 Diff 兼容 |
| `CellFormat.cs` | Excel 和 CSV 界面共用的基本格式结构 |
| `Tests/Editor/SpreadsheetInspectorTests.cs` | 真实 Editor 的 Undo、切换、批量格式和保存回归 |
| `Tests/Editor/SpreadsheetInspector.LayoutTests.cs` | 真实窗口中的高度限制、拖动恢复和左上角全选 |
| `Tests/Editor/SpreadsheetInspector.SelectionTests.cs` | 真实鼠标事件、四向持续滚动、松开停止和文本编辑隔离 |
| `Tests/Editor/SpreadsheetInspector.StructureTests.cs` | 批量插入的撤销、CSV 格式移动和真实加号点击 |

## Inspector 为什么反复绘制

IMGUI 不是只调用一次的页面。Unity 会针对布局、重绘、鼠标、键盘等事件反复执行 `OnInspectorGUI`，所以不能在这个方法里每次重新打开 Excel。`OnEnable` 打开文档，字段保存当前编辑状态，`OnDisable` 提交正在输入的文本、保留草稿并释放工作簿。

Unity 默认给 `DefaultAsset` Inspector 禁用 GUI。表格编辑的是原始文件，因此只在我们自己的绘制范围内启用 GUI，再根据文件是否只读控制写操作；`finally` 负责恢复 Unity 传入的状态。

## 单元格编辑如何保存

编辑栏和格内编辑器共用 `_editBuffer`。输入时先改变缓冲区，`_editChanged` 表示文本尚未交给文档。按 Apply、Enter、Tab、点击其他单元格或切换资源时，`CommitEdit` 调用文档的 `SetText`。

提交到文档后保存 `Library/XlsxInspector` 草稿，原始表格仍未改动。只有 **Save** 调用 `SpreadsheetDocument.Save`：比较原文件哈希，生成完整文件，再用同目录临时文件替换原件。没有待提交输入时，复制只读取文本，不设置文档的修改标记。

在最后一行列的预留空格输入后，`CommitEdit` 会立即重建网格，补出下一行列。`MoveSelection` 先提交输入，再限制目标坐标；如果顺序反过来，Tab 会按旧边界计算位置，第一次按键就无法走到新补出的空格。

CSV 的列分隔符由 `Csv.DetectFormat` 比较前 20 条逻辑记录的列数一致性推断，候选包括逗号、分号、制表符和竖线；记录换行符沿用第一条记录的方式。引号字段内部可以包含另一种换行，例如文件记录使用 LF，而某个单元格里含有 CRLF；这些内部换行不应改变文件的记录分隔方式。保存沿用识别出的分隔符，字段里的空格不会被擅自删除。

Excel 库会保留加载用的数据流。`_workbookStream` 必须与 `_workbook` 一起存活，否则第一次生成草稿之后再次保存就可能访问已经关闭的数据流。`Dispose` 同时关闭两者。

## 可变行列尺寸与坐标

`_columnEdges` 保存每列的左边界，最后一项是总宽度。例如三列宽度为 100、160、80，边界就是 `[0, 100, 260, 340]`。行使用同样的前缀坐标。

网格用二分查找从滚动距离定位第一条可见行和第一列，只画可见单元格。`GUI.BeginScrollView` 内使用表格内容坐标；表头放在滚动区域之外，绘制时减去滚动距离，所以行号和列字母可以固定。

鼠标按住表头边界时，`GUIUtility.hotControl` 记录谁正在处理拖动。拖动距离加上开始前的尺寸得到新尺寸。Excel 列宽由字符单位近似换算为像素，行高由磅换算为像素；CSV 没有格式信息，所以尺寸单独保存在 Unity 的 `EditorPrefs`，键包含项目路径和资源 GUID，不会加入 CSV 的单元格数据。

表头复用原生 label 文字样式，自行用 `EditorGUI.DrawRect` 铺满底色，再交给 `GUI.Button` 处理点击。这样行高变化时不会把单行工具栏贴图拉成深浅不一的背景，行列之间也能留出相同的 1 像素间隔。网格底部的调整条使用另一个 GUI 控件，只改变面板高度，不修改文件数据。

`CreateInspectorGUI` 将现有的 `OnInspectorGUI` 包在一个 `IMGUIContainer` 中，并绑定它真正所属的 Inspector 滚动视口。不能用 `focusedWindow` 推测窗口尺寸，因为焦点可能在 Project 或另一个 Inspector。高度上限使用视口高度减去工具栏和尾部的实际占用；先前存储的超大高度、窗口缩小和当前拖动都走同一条限制逻辑。

Inspector 外层可能因为旧的超大内容滚动到很下面，所以换算坐标时要加回外层滚动距离，使用内容原本的位置。这样不会把“已经滚出窗口”误认为多出一片可用空间。表格自己的滚动条只影响单元格内容，不影响这个上限。

拖动条通过 `GUIUtility.hotControl` 持有鼠标。单元格处理拖选前必须检查是否已有控件持有鼠标，否则向上拖动条时，鼠标一经过单元格就会被选区逻辑抢走 `MouseDrag`，造成只能拉大、无法拉回。

## 拖选如何越过可见区域

鼠标事件集中在 `SpreadsheetInspector.Selection.cs`。按下时先确定行列和选区起点，再用 `GUIUtility.hotControl` 捕获鼠标。这样指针离开数据区后，拖动和松开仍交给同一个网格处理；没有从网格开始的拖动不会建立选区。

`_selectionPointer` 保存鼠标相对于数据视口的位置，不使用单元格的滚动坐标。指针靠近四边时，根据距离计算水平和垂直速度，再用实际经过的时间推进 `_scroll`。两个方向分别计算，所以拖到角落也可以斜向滚动。速度有上限，长时间卡顿后的单次时间增量也有限制，避免重绘恢复时突然跳过很远。

鼠标停住时没有新的 `MouseDrag`，因此拖选期间临时订阅 `EditorApplication.update`，请求所属 IMGUI 容器重绘。滚动和选区更新仍在 `OnGUI` 内执行，不在 Editor 更新回调中读写 GUI 状态。每次滚动后，把鼠标位置限制到最近的可见边缘，再用已有的前缀坐标定位行列，只改变选区终点，保留起点。

仅按住而没有拖动时不自动滚动。松开、失去鼠标捕获或关闭 Inspector 时，释放控件并移除更新订阅。文字编辑、滚动条、行列尺寸和面板高度各自使用自己的鼠标捕获，不共用拖选状态。

## 批量插入为什么需要记录空白范围

菜单、数量输入区和加号都调用同一个 `InsertStructure`，在修改前校验数量，并只注册一次 Undo。Excel 使用 ClosedXML 的 `InsertRowsAbove(count)` / `InsertColumnsBefore(count)` 一次移动整个范围，公式和样式交给库维护；CSV 同步移动内容、尺寸数组和会话内单元格格式。

只用非空单元格推算表格大小时，末尾追加的空行列会在重新加载后消失。因此 Excel 按标准 `dimension` 记录工作区边界，读入时再与真实内容范围取较大值；CSV 使用空字段表示新增列，单列空行显式写成 `""`，避免被当成普通的文件结束换行。

加号在固定表头的最外侧绘制，不随单元格滚动；表头的显示范围和尺寸拖动边界都为它留出空间，避免加号与列宽、行高拖动热点重叠。

## 对齐和内容自适应

`CellFormat` 只描述网格需要的对齐和换行信息。Excel 格式通过 ClosedXML 的范围接口一次修改整个选区；没有显式设置的其他格式保持原样。CSV 的格式存入个人布局，插入删除行列时与内容一起移动。

自适应列宽使用当前 Unity 字体度量各行内容的最大宽度，自适应行高再根据新列宽计算换行高度。按这个顺序可以避免用旧列宽算出过高的行。

自适应可能从右键菜单回调中执行，因此通过 `EditorGUIUtility.GetBuiltinSkin` 显式取得 Inspector 样式，不假定其他窗口已经在 `OnGUI` 中初始化过 `EditorStyles`。

## 为什么撤销需要 ScriptableObject

Unity 的 Undo 记录的是 `UnityEngine.Object` 的序列化字段，普通 C# 工作簿对象不能直接交给 `Undo.RecordObject`。因此用 `SpreadsheetHistoryState` 保存可重新打开的文件字节、CSV 显示布局和操作位置，再由 `Undo.RegisterCompleteObjectUndo` 记录快照。原文件没有为撤销而创建额外资产。

一次批量操作只记录一次，拖动也在第一次实际移动时记录；鼠标只点一下边界不会产生修改。Undo 回调重新打开内存快照并更新草稿。资源切换时保留隐藏快照对象，所以返回资源后仍可撤销。

数据修订号决定内容是否需要保存，历史步骤号则包含 CSV 的显示格式修改。两者分开，能避免“只调整 CSV 列宽也提示文件被修改”。保存后的源文件哈希属于当前磁盘版本，不跟着 Undo 回到旧哈希，因此保存后撤销再保存不会误报外部修改。

历史节点保存操作目标。撤销时显示刚离开的操作位置，重做时显示新进入的位置；Sheet 标签切换属于视图动作，不添加撤销步骤。

## 本包的 C# 约定

- 四空格缩进，大括号独占一行，所有控制分支保留大括号。
- 类型、方法、属性和常量使用 PascalCase；私有实例字段使用 `_camelCase`；局部变量和参数使用 camelCase。
- 简短且无副作用的计算属性可以使用 `=>`；包含多个步骤、分支或状态修改的方法使用完整方法体。自动属性使用 `get;` / `set;`，不为它们创建没有作用的后备字段。
- 大段代码之前解释职责，私有字段末尾解释它保存什么状态。公开成员和主要内部数据 API 使用 XML 文档，参数说明索引是从 0 还是从 1 开始，有返回值时说明返回内容。
- 需要遵守 Unity 的生命周期与 GUI 状态恢复约定；`ExitGUIException` 属于 Unity 的控制信号，不能当成文件错误吞掉。
- `.editorconfig` 固定可自动执行的规则。注释解释原因、生命周期和转换关系，避免只把下一行代码再翻译一遍。

微软和 Unity 的指南提供可选择的风格，并不是一套完全相同的强制规则。例如 Unity 文章也介绍 `m_` / `k_` 前缀；本包统一采用微软的私有字段与常量命名，避免混用。

## 官方参考

- [Microsoft：C# 编码约定](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Microsoft：标识符命名](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [Microsoft：表达式体定义](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/lambda-operator#expression-body-definition)
- [Microsoft：XML 文档标记](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)
- [Unity：C# 命名和代码风格](https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity)

## 个人布局与项目默认值

`SpreadsheetInspector.Persistence.cs` 在资源关闭或切换 Sheet 时保存个人视图，CSV 尺寸与格式修改后立即更新。使用 EditorPrefs 的键包含当前项目路径哈希和资源 GUID；每张 Sheet 记录各自的滚动位置，重启 Editor 后仍能读取。撤销快照继续保存在会话内的隐藏对象中，不把整个工作簿塞进偏好设置。

Sheet 标签先通过 `GUILayoutUtility.GetRect` 获取固定宽度，再交给 `GUI.BeginScrollView` 裁切，标签总宽度不会撑大外层 Inspector。横向参数使用 `GUI.skin.horizontalScrollbar`，纵向使用 `GUIStyle.none`；两者顺序不能写反。活动 Sheet 改变或窗口宽度变化时，把相应标签滚入视口。CSV 在申请布局空间之前直接返回，底部高度重新测量后，网格可使用释放的空间。

IMGUI 控件按调用顺序取得 ID，而 `GUI.BeginScrollView` 会按 ID 保存内部滚动状态。可见行号、列标题的按钮数量随面板高度改变，因此不能把 Sheet 滚动控件放在它们后面创建。现在先申请网格、拖动条与标签栏的矩形，再画标签控件，最后画网格；位置仍在底部，控件 ID 却不再受可见行列数量影响。拖动当帧不重新读取已经求解的 GUILayout 矩形，下一轮 Layout 才应用新的高度。

`SpreadsheetSettings` 复用 Unity 的 ScriptableSingleton 和 SettingsProvider，将团队可共享的设置保存到 `ProjectSettings/UnityXlsxInspector.asset`。默认 CSV 尺寸、面板高度与初始预览行列数用于尚未保存个人布局的文件；字号、配色、交替底色、悬浮提示、操作说明与拖选速度在当前界面立即生效。显示字号同时用于内容自适应测量，不写入工作簿字体。设置页改用 UI Toolkit 的 `PropertyField` 和序列化绑定，tooltip 放在整个字段上，覆盖标签和输入区；嵌套颜色字段通过 `TooltipAttribute` 提供逐项说明。

`SpreadsheetInspector.Preview.cs` 在视口接近当前边缘时补出下一屏网格，方向键进入最后一行列时也走同一个扩展入口。只绘制可见格子，不一次创建整张 Excel 的百万行。CSV 尺寸数组只在用户真正调整尺寸时才分配，Excel 先读取已定义的行列尺寸，避免浏览空白区就创建大量行列对象。原始文件允许的最大行列数仍是预览的上限。

预览回收在 Repaint 中进行，并等待 `GUIUtility.hotControl == 0`，避免鼠标按住滚动条时修改其比例。保留实际内容、当前可见区域和视口外一屏，释放更远的灰色尾部；只改变坐标数组，不删单元格或格式。选区保存的是文档坐标，不跟着预览回收改变；F2、方向键或撤销需要显示它时，`RevealSelection` 先恢复所需坐标。Layout 事件中的矩形尚未求解，不能据此更新视口尺寸或预览范围。

插入和删除用于移动已有数据，并非为网格扩容。表头加号在选区下方或右侧插入，右键打开对应的批量设置。全选使用实际内容边界；清空保留位置，删除行列才移动后续数据。`SpreadsheetPalette` 统一保存绘制用颜色，内置方案为深浅皮肤生成不同底色；Custom 分别保存两套颜色，避免一组背景和文字同时用于两种皮肤。

亮色区域由 `ContentRowCount` 和 `ContentColumnCount` 计算，CSV 查找最后一个非空字符串，Excel 使用 `XLCellsUsedOptions.Contents`，因此单纯的格式和追加空白不会扩大它。公式和包含空格的文本仍算内容；范围内部的空格保持原位置。`RebuildGrid` 缓存边界，输入、清空、粘贴、Undo 和重新加载都会更新，不必再提供手动 Trim。灰色格仍走原来的编辑和粘贴入口，无需先插入行列。

## 为什么要规范 XML 命名空间

OOXML 中 `x:sheetData` 和默认命名空间下的 `sheetData` 表示同一个元素。部分表格 Diff 工具只搜索无前缀的标签，遇到前者可能把整张表读成空白。写出文件后，统一 SpreadsheetML 主命名空间为默认绑定，保留其他前缀声明和扩展内容。验证要同时使用 ClosedXML 和目标 Diff 解析器，不能只看 ZIP 是否有效。
