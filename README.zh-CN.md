# Unity Xlsx Inspector

[English](README.md) | 简体中文

直接在 Unity Inspector 中编辑 `.xlsx` 和 `.csv`。在 Project 选中文件，Inspector 面板中修改单元格，再保存回原文件。

![Inspector 中的 XLSX 编辑](image/README.zh-CN/1793945502651.png)

## 环境要求

- Unity **2022.3 LTS 及以上**，支持 Unity 6。
- API Compatibility Level 使用 **.NET Standard 2.1**。
- 仅供 Editor 使用的 UPM 包，已包含依赖。

| 实测 Editor | 平台    | 验证          |
| ----------- | ------- | ------------- |
| 2022.3.62f3 | Windows | EditMode 测试 |
| 6000.0.70f1 | Windows | EditMode 测试 |

## 安装

打开 **Window → Package Manager → + → Install package from git URL**，输入：

```text
https://github.com/zheliku/UnityXlsxInspector.git?path=/Packages/com.zheliku.unity-xlsx-inspector
```

也可以把 `Packages/com.zheliku.unity-xlsx-inspector` 复制到工程的 `Packages` 目录。

## 功能

### 单元格编辑与复制粘贴

双击单元格或按 **F2** 编辑。**Enter** 提交并向下移动，**Tab** 向右移动，**Esc** 取消当前输入。上方编辑栏可输入多行文本。

拖动或 Shift+点击选择区域，支持复制、剪切、粘贴、清空、Undo 和 Redo，切换资源后仍可撤销。直接往空格粘贴即可扩展表格。点击 **Save** 写回原文件；未保存内容会保留为本地草稿，重启 Editor 后可继续编辑。

![双击编辑单元格](image/cell-editing.png)

### Sheet 切换

XLSX 页签限制在 Inspector 宽度内，可用滚轮或横向滚动条浏览。**‹ / ›** 切换前后 Sheet，**▾** 打开完整列表。切换时，活动页签会自动进入可见区域。CSV 不显示 Sheet 栏。

![多 Sheet 导航](image/worksheet-tabs.png)

### 行列操作与尺寸

**Rows / Columns** 支持单个或批量插入、删除行列。行号旁的 **+** 在选区下方插入，列标题旁的 **+** 在选区右侧插入；右键加号可设置数量。插入、删除会移动后续数据，**Clear** 则只清空内容、保留位置。

![批量插入行](image/batch-insert.png)

拖动表头边界调整行高列宽，或输入 **Width / Height** 统一设置整个选区。**Auto Fit** 按内容自适应；双击表头边界可自适应对应行或列。**Format** 和单元格右键菜单提供水平、垂直对齐与自动换行。

网格下方的横条可拖动调整面板高度。亮色区域表示实际内容范围，灰色区域仍可编辑。滚动、拖选时按需扩展空白预览；返回后松开鼠标，会回收视口外多余的灰格，让滚动条滑块恢复长度。有内容的范围会保留。

![选区尺寸、对齐与换行](image/range-formatting.png)

### CSV 与搜索

CSV 自动识别**逗号、分号、Tab 和竖线 `|`**。保存时保留分隔符、编码、记录换行和字段空格。支持有无 BOM 的 UTF-8，以及带 BOM 的 UTF-16/32；带引号的字段可以包含分隔符、双引号和换行。

搜索不区分大小写，匹配单元格会显示标记，**Find Next** 在当前 Sheet 中依次定位。

![竖线分隔 CSV 的搜索](image/search.png)

### 布局与配色

打开 **Edit → Project Settings → Unity Xlsx Inspector**，也可点击 Inspector 工具栏的 **Settings**。

| 分组                | 设置                                                 |
| ------------------- | ---------------------------------------------------- |
| Layout              | CSV 默认尺寸、面板高度、初始预览行列数               |
| Appearance          | 网格字号、交替行底色、配色方案、单元格提示、快捷说明 |
| Selection scrolling | 拖选滚动速度和边缘触发距离                           |

**Forest、Ocean、Plum、Graphite** 四套配色跟随 Unity 深浅主题。选择 **Custom** 可分别编辑两种主题的背景、表头、网格线、文字、强调色与搜索标记。悬停设置项可查看说明。

![项目设置](image/settings.png)

CSV 显示格式、面板高度、当前 Sheet 和滚动位置按项目、资源保存在本机。团队共享的默认值位于 `ProjectSettings/UnityXlsxInspector.asset`。XLSX 行高列宽、对齐写入工作簿；网格字号和配色只影响 Inspector。

![Dark 主题](image/dark-theme.png)

## 快捷键

| 按键                      | 操作                              |
| ------------------------- | --------------------------------- |
| F2 / Enter                | 编辑当前单元格                    |
| Enter / Shift+Enter       | 提交，向下 / 向上移动             |
| Tab / Shift+Tab           | 提交，向右 / 向左移动             |
| Esc                       | 取消当前单元格输入                |
| Shift+点击 / Shift+方向键 | 扩展选区                          |
| Ctrl+A / C / X / V        | 选择内容范围 / 复制 / 剪切 / 粘贴 |
| Delete / Backspace        | 清空选中内容                      |
| Ctrl+S / Z                | 保存 / 撤销                       |
| Ctrl+Y / Ctrl+Shift+Z     | 重做                              |

macOS 使用 **Cmd** 代替 Ctrl。文本框获得焦点时，剪贴板快捷键作用于框内文字。

## 文件行为

- XLSX 保留数字和布尔类型，`001` 按文本处理。前置 `'` 强制文本，`=` 输入公式。公式预览使用缓存结果，Excel/WPS 打开后重新计算。
- 网格显示单元格内容、基本对齐、换行和尺寸，不渲染合并布局、工作簿字体、图表和条件格式。工作簿读写使用 ClosedXML。
- 草稿位于 `Library/XlsxInspector`；Undo 历史保留在当前 Editor 会话。**Reload** 会询问是否放弃编辑。保存时检查外部文件变化；遇到冲突或文件锁定，会保留待保存内容。
- 工作簿和撤销快照加载在内存中，本包面向配置表编辑。缓存包中的资源只读。

CSV 检测采样前 20 条逻辑记录；存在歧义时，可先统一文件分隔符。[RFC 4180](https://www.rfc-editor.org/rfc/rfc4180) 描述逗号分隔的 CSV，分号、Tab 和竖线是本包支持的方言，参见 [CSVW](https://www.w3.org/TR/tabular-metadata/#dialect-descriptions)。

## 开发

源码位于 `Packages/com.zheliku.unity-xlsx-inspector`。安装 Unity Test Framework，在工程 manifest 中加入 `"testables": ["com.zheliku.unity-xlsx-inspector"]`，然后运行 **Test Runner → EditMode → XlsxInspector.Tests**。界面测试需要启用图形的 Editor 和系统剪贴板。

[代码导读](Packages/com.zheliku.unity-xlsx-inspector/Documentation~/code-guide.md) · [更新记录](Packages/com.zheliku.unity-xlsx-inspector/CHANGELOG.md) · [发布步骤](Packages/com.zheliku.unity-xlsx-inspector/Documentation~/releasing.md)

## 许可

项目代码使用 [MIT](LICENSE.md)。随包提供的 ClosedXML、Open XML SDK、ExcelNumberFormat、RBush 和两个 System 程序集均使用 MIT。**SixLabors.Fonts 1.0.0 使用 Apache-2.0**，ClosedXML 内嵌的 **CarlitoBare 字体数据使用 SIL OFL 1.1**。对应的完整许可与声明随包保留。

依赖版本、上游地址和完整许可见 [Third Party Notices](<Packages/com.zheliku.unity-xlsx-inspector/Third%20Party%20Notices.md>)。
