# UCAD v0.3.7 — UI Fidelity & HiDPI Foundation

v0.3.7 是进入 v0.4.0 深度 UI ↔ CAD Core 耦合之前的界面与显示基础版本。本次不新增 CAD Core 命令，重点解决 v0.3.5/0.3.6 实机界面在高 DPI 环境下发虚、与已确认 Figma v0.2 母版差异较大的问题，并把后续交互能力所依赖的工作区外壳固定下来。

## HiDPI 清晰度

- 恢复应用清单中的 `PerMonitorV2` DPI awareness，并保留 `true/pm` 兼容声明，避免自定义 manifest 覆盖 WinUI 默认高 DPI 配置后由 Windows 对整个窗口做位图缩放。
- CI 新增 HiDPI 静态契约：只要 `PerMonitorV2` 被误删即直接失败。
- 保留 v0.3.6 的真实启动烟雾测试，继续在 Windows runner 上实际启动 UCAD 并检查启动日志。

## Figma v0.2 高保真工作区

- 将已确认的 1440×900 Figma v0.2 中文桌面界面转换为 WinUI 3 工作区基线。
- 标题栏改为浏览器式多文档结构：固定 UCAD 品牌区，图纸标签约 `190×34`，标签直接从品牌区右侧连续排列。
- 保留原生 WinUI 标题栏行为与系统窗口按钮，不用截图或自绘假标题栏替代。
- 顶部固定为 `文件 / 编辑 / 视图 | 绘图 / 修改 / 注释 / 图层 / 图块 / 测量 / 视图` 分类栏。
- 分类工具架保持“点击一次展开并持续显示；再次点击当前分类才收起”的 CAD 工作流。
- 左侧 52 px Tool Rail 继续只承担最高频命令入口。
- Inspector、命令行、状态栏按 Figma 的尺寸、层级和暗色表面重新整理。

## 设计 Token 与图标

- 新增 `UcadDesignTokens.xaml`，将 Figma 的标题栏、分类栏、工具架、Inspector、Canvas、状态栏、文字、分隔线和 Accent 颜色集中为可复用 WinUI 资源。
- Cursor 使用 Microsoft Fluent System Icons 的矢量路径；Move、Copy、Trim、More 等通用命令使用 Fluent 系统图标语义。
- Line、Polyline、Offset 等 CAD 专业图标暂时保留现有简化符号，待后续建立 UCAD 专用 CAD Fluent Icons，不用错误的通用系统图标硬替代。
- 未实现分类不再使用 WinUI 默认 `Disabled` 灰化整个分类文字；分类本身保持清晰可切换，未实现工具则明确显示为预留状态。

## Shell 行为与版本状态

- 多文档标签继续对应真实独立的 `CadWorkspaceSession`，没有退回为视觉假标签。
- LINE / PLINE / RECTANGLE / CIRCLE / ARC、Undo / Redo、Clear、Reset View 继续沿用现有统一命令路径。
- 状态栏版本号改为从程序集版本动态生成，删除 `UCAD v0.3.5` 之类的硬编码。
- zh-CN、ja-JP、en-US 三套资源同步补齐工具架预留状态，并清理部分过时的 v0.3.5 文案。

## CI 防回归

v0.3.7 在现有 Core tests、app-build、startup-smoke、MSIX / one-click validation 之外增加界面基础契约：

- 必须保留 `PerMonitorV2`；
- Shell 不得重新硬编码 v0.3.x 显示版本；
- 浏览器式标题栏不得重新注入单独的 `AppTitleBar.Title`；
- Figma 工作区所依赖的核心 UCAD design tokens 必须存在；
- 运行时资源键继续要求 zh-CN / ja-JP / en-US 三套 RESW 全覆盖。

## 范围

本版本不实现 Selection、OSNAP、Ortho、Move、Copy、Offset 或 Trim 的 CAD Core 功能。v0.4.0 将直接在 v0.3.7 固定下来的 `CadWorkspaceSession + CadViewport + Inspector + StatusBar` 边界上接入选择、捕捉、正交及属性联动。
