# 发布检查

UPM 包位于 `Packages/com.zheliku.unity-xlsx-inspector`。仓库里的 Unity 工程用于开发，不作为包的一部分发布。

## 版本与文档

1. 确定版本号，更新 `package.json`。把 `CHANGELOG.md` 的 Unreleased 内容归入对应版本，填写实际发布日期。
2. 根目录 `README.md` 是英文主页，`README.zh-CN.md` 是独立的中文说明；同步核对两份文档的安装地址、操作名称、截图和支持范围。包内 README 仅保留安装入口，不再维护另一份完整使用文档。
3. 核对 `documentationUrl`、`changelogUrl` 和 `licensesUrl` 指向实际存在的仓库分支和文件。
4. 在声明的最低 Unity 版本和当前维护版本分别检查。如果尚未验证某个版本或平台，在 README 中写清楚，不把包元数据当成测试结果。

## 功能验证

在安装了 Unity Test Framework 的测试工程中，将包名加入 `Packages/manifest.json` 的 `testables`，运行 EditMode 下的 `XlsxInspector.Tests`。

```json
"testables": ["com.zheliku.unity-xlsx-inspector"]
```

测试使用临时文件。窗口布局测试需要图形模式；剪贴板测试需要系统剪贴板可用。检查失败原因和测试报告，不能只看脚本编译是否成功。

再用工作簿副本手动检查一次：

- 选中 xlsx / csv，原有图标与普通文本 Inspector 显示正常。
- 双击编辑、Sheet 切换、复制粘贴、搜索，以及保存重开。
- 批量增删、表头加号、拖动尺寸、Auto Fit、对齐和换行。
- 撤销重做、切换资源后的草稿、保存后撤销再保存。
- 缩小 Inspector、向上拖动高度条，确认底部标签仍可操作。

## 依赖与许可

- `Editor/Plugins` 的每个 DLL 都应只对 Editor 启用；测试程序集不应成为普通安装的依赖。
- 更新 DLL 时，从对应 NuGet 版本取程序集，同时更新 `Third Party Notices.md` 和完整许可证。表格记录 NuGet 版本，不能只根据程序集版本号猜测。
- 不重复打包 Unity 已提供的 System.Memory、System.Buffers 等程序集。
- 保留根目录和包内的 MIT 许可证，以及 `ThirdPartyNotices~` 中的第三方许可。SixLabors.Fonts 1.0.0 是 Apache-2.0，Carlito 字体数据是 SIL OFL 1.1。
- 包内 `.gitattributes` 将 DLL 按普通二进制提交。检查 Git 中的 DLL 是实际程序集，而不是 Git LFS 指针；否则 UPM Git 安装可能缺少依赖。

## 干净安装

在没有本项目 Library 缓存的新工程中，通过 Git URL 安装准备发布的提交，确认能编译和打开一份 xlsx / csv。普通安装无需启用 `testables`，也不应要求使用者另装 NuGet 或测试框架。

确认包里的脚本、程序集、JSON 和非忽略文档均带有 `.meta`，没有重复 GUID。`Documentation~`、`ThirdPartyNotices~` 和以点开头的配置文件由 Unity 忽略，不需要 `.meta`。

## 创建版本

以上检查完成后，提交包内容，按版本创建 Git 标签和 GitHub Release。首个版本为 1.0.0，使用标签 `v1.0.0`，在发布说明中附上固定版本的安装地址：

```text
https://github.com/zheliku/UnityXlsxInspector.git?path=/Packages/com.zheliku.unity-xlsx-inspector#v1.0.0
```

标签存在后这个地址才能安装。不要移动已经发布的标签；后续修复使用新版本。README 中不带标签的地址用于跟随默认分支。
