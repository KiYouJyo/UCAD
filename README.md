[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
面向城市规划与建筑设计的轻量二维 CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 当前定位

UCAD 目标是建立一套 Windows 原生、接近 AutoCAD 操作习惯、面向建筑与规划高频二维制图任务的轻量 CAD。坚持 DXF-first、2D-first，不以复制完整 AutoCAD 为目标。

**当前候选版本 v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation。** 本版本不增加新的 CAD Core 能力，而是以 1440×900 Figma 文件作为视觉 SSOT，完成浏览器式文档标签、Start Center、完整 Settings、三语资源、Fluent 图标、设计 Token、版本 SSOT 与运行时截图验收，为 v0.4.0 的 Selection / OSNAP / Inspector 继续开发冻结一套稳定 UI 基础。

## 安装

正式版本从 [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) 下载：

- `UCAD-v<version>-x64-one-click.zip`：普通用户推荐。
- `UCAD_<packageVersion>_x64.msixbundle`：高级用户直接侧载。
- `SHA256SUMS.txt`：完整性校验。

首次 one-click 安装仅在建立 `LocalMachine\TrustedPeople` 公钥信任时触发一次 UAC；MSIX 本体仍在当前用户上下文安装。

## 工作区与页面

UCAD 当前明确区分三种标签内容：

- **Drawing**：显示 Category Bar、Tool Shelf、Tool Rail、CAD Canvas、Inspector、Command Line 与 Status Bar。
- **Start**：作为 CAD 新标签页 / Start Center；标题栏 `+` 默认进入 Start，只有点击“新建图纸”才创建真正的 `CadWorkspaceSession`。
- **Settings**：作为单例设置标签；不与 CAD Tool Rail / Inspector / Command Line / Status Bar 同时出现。

Start 提供新建、打开入口、最近使用空状态、Blank / Architecture / Urban Planning 模板信息架构和 Learn UCAD。文件打开/保存与专业模板业务尚未实现的部分会明确保持占位状态，不伪造成可用功能。

## 当前命令

每个 Drawing 标签都是独立的内存 CAD 会话，分别保存自己的图形、Undo/Redo、命令状态和视图状态。当前尚未实现文件保存/打开，因此关闭含有绘图内容的标签会先提示内容将丢失。

| 命令 | 别名 | 功能 |
| --- | --- | --- |
| `LINE` | `L` | 连续直线 |
| `PLINE` | `PL` | 多段线 |
| `RECTANGLE` | `REC` | 两角点矩形 |
| `CIRCLE` | `C` | 圆心/半径圆 |
| `ARC` | `A` | 三点圆弧 |
| `UNDO` | `U` | 撤销 |
| `REDO` | — | 重做 |
| `CLEAR` | — | 清空图形 |
| `RESETVIEW` | `RV` | 重置视图 |

所有入口最终统一路由到 `CommandRegistry → CommandSession → CAD Core`。选择、MOVE/COPY/OFFSET/TRIM、图层、OSNAP、ORTHO 等规划中的能力已经预留在 UI 信息架构中，但对应 CAD Core 未实现前不会伪装成可用命令。

## Settings 与显示基线

- WinUI 3 / Windows App SDK 原生窗口，`PerMonitorV2` 高 DPI awareness；
- 1440×900 Figma 文件为 UI 视觉 SSOT；
- 标题栏 44、分类栏 44、工具架 64、Tool Rail 52、Inspector 304、命令行 34、状态栏 30 DIP；
- Settings 左侧导航 228 DIP、内容起点 54 DIP、卡片 940×72 DIP，采用 35 / 12 / 8 / 30 DIP 纵向节奏；
- App Theme 与 CAD Canvas Theme 独立；画布背景、栅格显示/强度、光标中心缩放、中键平移和滚轮方向通过集中设置层进入现有 Viewport；
- 通用动作优先使用 Fluent / WinUI 原生图标，CAD 专业图标使用 UCAD Fluent 风格 `PathIcon`；
- 设置集中由 `SettingsService` / `AppSettings` 管理，并保存到 `%LOCALAPPDATA%\UCAD\settings.json`。

## 三语支持

应用与仓库使用简体中文（zh-CN）、日本語（ja-JP）、English（en-US）三语资源。v0.3.9 的 Start 与全部 Settings 页面保持三语 key 一一对应。

## 仓库结构

```text
src/UCAD.Core/          几何、实体、文档历史与命令核心
src/UCAD.App/           WinUI 3 / Win2D Shell、页面、交互、渲染与 MSIX
tests/                  自动化测试
packaging/              一键安装与发布校验
release/                发布 SSOT 元数据
docs/                   架构与三语 Release Notes
.github/workflows/       CI、UI 截图验收与 GitHub Release
```

## 开发

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

CI 覆盖 Core tests、app-build、真实启动 smoke、MSIX/package validation、三语资源契约、版本 SSOT、PerMonitorV2、Unicode 占位图标扫描与 1440×900 UI 截图产物。

## 文档

- [路线图](ROADMAP.md)
- [架构说明](docs/ARCHITECTURE.md)
- [发布流程](docs/RELEASE-PROCESS.md)
- [打包说明](packaging/README.md)
- [v0.3.9 发布说明](docs/RELEASE-NOTES-v0.3.9.md)

## 许可证

UCAD 以 **GPL-2.0-only** 发布。
