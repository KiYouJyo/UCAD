[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
面向城市规划与建筑设计的轻量二维 CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 当前定位

UCAD 目标是建立一套 Windows 原生、接近 AutoCAD 操作习惯、面向建筑与规划高频二维制图任务的轻量 CAD。坚持 DXF-first、2D-first，不以复制完整 AutoCAD 为目标。

**当前候选版本 v0.4.1 — CAD Selection & Cursor Interaction Refinement。** 本版本继续暂停像素级 UI 精调，重点把 v0.4.0 的 Selection / OSNAP / Ortho 基础交互调整得更接近传统 CAD：空白单击后可用第二点完成 Window/Crossing 框选，同时保留拖拽框选；Shift 可减选；绘图区只显示 UCAD 自绘的 CAD 十字准线与可调 Pickbox，不再叠加 Windows 小十字。

## 安装

正式版本从 [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) 下载：

- `UCAD-v<version>-x64-one-click.zip`：普通用户推荐。
- `UCAD_<packageVersion>_x64.msixbundle`：高级用户直接侧载。
- `SHA256SUMS.txt`：完整性校验。

首次 one-click 安装仅在建立 `LocalMachine\TrustedPeople` 公钥信任时触发一次 UAC；MSIX 本体仍在当前用户上下文安装。

## 工作区与页面

UCAD 明确区分三种标签内容：

- **Drawing**：显示 Category Bar、Tool Shelf、Tool Rail、CAD Canvas、Inspector、Command Line 与 Status Bar。
- **Start**：作为 CAD 新标签页 / Start Center。默认情况下标题栏 `+` 进入 Start，点击“新建图纸”才创建真正的 `CadWorkspaceSession`；关闭“新标签页显示开始页”后，`+` 会直接创建空白 Drawing。
- **Settings**：作为单例设置标签；不与 CAD Tool Rail / Inspector / Command Line / Status Bar 同时出现。

每个 Drawing 都有独立的 `CadDocument`、`CadInteractionState`、`CadViewport` 与 `CommandSession`，因此多文档的图形、Undo/Redo、选择、OSNAP、Ortho、命令与视口状态彼此独立。

## v0.4.1 交互

- 单击对象选择；继续单击其他对象可累加多选。
- Shift + 单击或 Shift + 框选可从当前选择集中移除对象。
- 空白处单击第一角后可松开鼠标，移动预览选框，再次单击提交；按住拖拽后松开的快速框选仍然保留。
- 左→右为 **Window**：只选择完全包含的对象；右→左为 **Crossing**：包含或相交的对象都会选择。
- 完成一个空的非 Shift 选框会清除当前选择；Esc 优先取消尚未完成的选框，再清除已完成选择。
- 绘图区隐藏 Windows 原生指针，仅保留 Win2D 绘制的十字准线 + 中央 Pickbox；十字准线 5–100%，Pickbox 3–20 px（默认 10 px），OSNAP aperture 3–50 px（默认 10 px），均可在“设置 → 输入与交互 → CAD 光标”实时调整。
- **F3 / 状态栏 OSNAP**：切换对象捕捉；当前基础集合为端点、中点、圆心、交点。
- **F8 / 状态栏 ORTHO**：切换 LINE / PLINE 鼠标输入的水平/垂直约束。
- **Delete / ERASE / E / DELETE**：删除当前选择；多对象删除是一个 Undo 步骤。
- Inspector 会读取真实的 Line / Polyline / Circle / Arc 选择，显示类型、数量、基础几何与实体 ID。

## 当前命令

| 命令 | 别名 | 功能 |
| --- | --- | --- |
| `LINE` | `L` | 连续直线 |
| `PLINE` | `PL` | 多段线 |
| `RECTANGLE` | `REC` | 两角点矩形 |
| `CIRCLE` | `C` | 圆心/半径圆 |
| `ARC` | `A` | 三点圆弧 |
| `ERASE` | `E`, `DELETE` | 删除当前选择 |
| `UNDO` | `U` | 撤销 |
| `REDO` | — | 重做 |
| `CLEAR` | — | 清空图形 |
| `RESETVIEW` | `RV` | 重置视图 |

所有命令入口统一路由到 `CommandRegistry → CommandSession → CAD Core`。Modify / Annotate / Layer / Block / Measure 等分类的可用状态开始从真实注册能力推导；对应 Core 尚未存在时不会伪装成可用功能。MOVE / COPY / ROTATE / OFFSET / TRIM 等属于 v0.5.x。

## Settings 与显示基线

- WinUI 3 / Windows App SDK 原生窗口，`PerMonitorV2` 高 DPI awareness；
- Figma 1440×900 Frame 继续作为视觉 SSOT，但 v0.4.x 不以像素级 UI 精校作为发布门槛；
- App Theme 与 CAD Canvas Theme 独立生效；
- 栅格显示/强度、光标中心缩放、中键平移、滚轮方向、选择预览和坐标格式进入运行时逻辑；
- “默认对象捕捉 / 默认捕捉类型 / 默认正交”会初始化之后新建的 Drawing；现有 Drawing 的 F3/F8 状态不会被默认设置强制覆盖；
- 尚无底层能力的 Restore Session、手动 UI Scale、自动更新、最近文件清理等不会显示成“已实现”；
- 设置集中由 `SettingsService` / `AppSettings` 管理，并保存到 `%LOCALAPPDATA%\UCAD\settings.json`。

## 三语支持

应用与仓库使用简体中文（zh-CN）、日本語（ja-JP）、English（en-US）三语资源。独立 MRT Core `ResourceContext` 支持**无需重启**切换语言，并立即刷新当前 Window、Start、Settings、文档标签、菜单、Inspector、命令区与状态栏；现有图形、选择会话、Undo/Redo 与视口状态不会因为语言切换被重建。

## 仓库结构

```text
src/UCAD.Core/          几何、实体、文档历史、命令与交互核心
src/UCAD.App/           WinUI 3 / Win2D Shell、页面、交互、渲染与 MSIX
tests/                  自动化测试
packaging/              一键安装与发布校验
release/                发布 SSOT 元数据
docs/                   架构与三语 Release Notes
.github/workflows/       CI 与发布工作流
```

## 开发

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

CI 强制覆盖 Core tests、app-build、真实 startup-smoke、MSIX/package validation、三语资源契约、版本 SSOT、PerMonitorV2、Unicode 占位图标扫描与冻结的 Figma 关键 Design Tokens。Interaction Smoke 会在真实运行的 UCAD 内验证 Selection、ERASE、OSNAP、Ortho 与 Inspector；Localization Smoke 继续在同一进程依次切换 zh-CN → ja-JP → en-US。

像素级 Figma 对照保留在手动 `UI Fidelity Screenshots` 工作流中，不阻塞 v0.4.x 功能开发或发布。

## 文档

- [路线图](ROADMAP.md)
- [架构说明](docs/ARCHITECTURE.md)
- [发布流程](docs/RELEASE-PROCESS.md)
- [打包说明](packaging/README.md)
- [v0.4.1 发布说明](docs/RELEASE-NOTES-v0.4.1.md)

## 许可证

UCAD 以 **GPL-2.0-only** 发布。