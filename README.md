[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
面向城市规划与建筑设计的轻量二维 CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 当前定位

UCAD 目标是建立一套 Windows 原生、接近 AutoCAD 操作习惯、面向建筑与规划高频二维制图任务的轻量 CAD。坚持 DXF-first、2D-first，不以复制完整 AutoCAD 为目标。

**当前版本 v0.3.0 — Drawing Foundation。** 已形成第一套完整绘图闭环：AutoCAD 风格命令输入、LINE / PLINE / RECTANGLE / CIRCLE / ARC、鼠标与坐标混合输入、实时预览，以及文档级 Undo / Redo。

## 安装

从 [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) 下载：

- `UCAD-v0.3.0-x64-one-click.zip`：普通用户推荐。
- `UCAD_0.3.0.0_x64.msixbundle`：高级用户直接侧载。
- `SHA256SUMS.txt`：完整性校验。

首次 one-click 安装仅在建立 `LocalMachine\TrustedPeople` 公钥信任时触发一次 UAC；MSIX 本体仍在当前用户上下文安装。

## 当前命令

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

点输入支持 `x,y`、`@x,y` 与已有基点后的距离；Enter / Space 确认，Esc 取消，空输入确认可重复上一命令。

## 三语支持

应用与仓库使用简体中文（zh-CN）、日本語（ja-JP）、English（en-US）三语资源。

## 仓库结构

```text
src/UCAD.Core/          几何、实体、文档历史与命令核心
src/UCAD.App/           WinUI 3 / Win2D 交互、渲染与 MSIX
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
- [v0.3.0 发布说明](docs/RELEASE-NOTES-v0.3.0.md)

## 许可证

UCAD 以 **GPL-2.0-only** 发布。
