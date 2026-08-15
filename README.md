[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
面向城市规划与建筑设计的轻量二维 CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 当前定位

UCAD 目标是建立一套 Windows 原生、接近 AutoCAD 操作习惯、面向建筑与规划高频二维制图任务的轻量 CAD。坚持 DXF-first、2D-first，不以复制完整 AutoCAD 为目标。

**当前候选版本 v0.3.7 — UI Fidelity & HiDPI Foundation。** 在 v0.3.5/0.3.6 已建立的多文档工作区与 UI↔Core 状态桥之上，v0.3.7 恢复 PerMonitorV2 高 DPI 支持，并将实际 WinUI 3 Shell 高保真对齐已确认的 Figma v0.2：浏览器式图纸标签、持久分类工具架、左侧高频 Tool Rail、Inspector、命令行与状态栏。此版本不新增 CAD Core 命令，为 v0.4.0 的 Selection / OSNAP / Ortho 奠定稳定显示与交互壳层。

## 安装

正式版本从 [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) 下载：

- `UCAD-v<version>-x64-one-click.zip`：普通用户推荐。
- `UCAD_<packageVersion>_x64.msixbundle`：高级用户直接侧载。
- `SHA256SUMS.txt`：完整性校验。

首次 one-click 安装仅在建立 `LocalMachine\TrustedPeople` 公钥信任时触发一次 UAC；MSIX 本体仍在当前用户上下文安装。

## 当前工作区与命令

每个顶部标签都是独立的内存 CAD 会话，分别保存自己的图形、Undo/Redo、命令状态和视图状态。当前尚未实现文件保存/打开，因此关闭含有绘图内容的标签会先提示内容将丢失。

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

同一命令可从顶部工具架、左侧高频栏、命令搜索或底部命令行进入，最终统一路由到 `CommandRegistry` / `CommandSession`。点输入支持 `x,y`、`@x,y` 与已有基点后的距离；Enter / Space 确认，Esc 取消，空输入确认可重复上一命令。

选择、MOVE/COPY/OFFSET/TRIM、图层、OSNAP、ORTHO 等规划中的能力已经预留在 UI 信息架构中，但对应 CAD Core 未实现前不会伪装成可用命令。

## 显示与界面基线

- WinUI 3 / Windows App SDK 原生窗口；
- `PerMonitorV2` 高 DPI awareness；
- 1440×900 Figma v0.2 为当前 Shell 视觉基线；
- 标题栏 44 px、分类栏 44 px、工具架 64 px、Tool Rail 52 px、Inspector 304 px、命令行 34 px、状态栏 30 px；
- 通用动作优先使用 Fluent 系统图标，CAD 专业图标逐步建立 UCAD 自有 Fluent 扩展集。

## 三语支持

应用与仓库使用简体中文（zh-CN）、日本語（ja-JP）、English（en-US）三语资源。

## 仓库结构

```text
src/UCAD.Core/          几何、实体、文档历史与命令核心
src/UCAD.App/           WinUI 3 / Win2D 工作区、交互、渲染与 MSIX
tests/                  自动化测试
packaging/              一键安装与发布校验
release/                发布 SSOT 元数据
docs/                   架构与三语 Release Notes
.github/workflows/       CI 与签名 GitHub Release
```

## 开发

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

## 文档

- [路线图](ROADMAP.md)
- [架构说明](docs/ARCHITECTURE.md)
- [发布流程](docs/RELEASE-PROCESS.md)
- [打包说明](packaging/README.md)
- [v0.3.7 发布说明](docs/RELEASE-NOTES-v0.3.7.md)

## 许可证

UCAD 以 **GPL-2.0-only** 发布。
