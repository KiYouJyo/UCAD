[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
面向城市规划与建筑设计的轻量二维 CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0-blue)

## 当前定位

UCAD 目标是建立一套 Windows 原生、接近 AutoCAD 操作习惯、面向建筑与规划高频二维制图任务的轻量 CAD。项目坚持 DXF-first、2D-first，不以复制完整 AutoCAD 为目标。

v0.1.0 是 Foundation Release：目前已经具备 Win2D 绘图视口、网格、坐标变换、缩放 / 平移和基础 Line 实体；后续将逐步加入命令系统、选择与 OSNAP、Trim / Offset 等修改工具、图层、标注、DXF 和规划专属能力。

## 安装

推荐从 [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) 下载：

- `UCAD-v0.1.0-x64-one-click.zip`：普通用户推荐。解压后双击 `① 安装UCAD.cmd`，自动校验并安装签名 MSIX。
- `UCAD_0.1.0.0_x64.msixbundle`：高级用户直接侧载包。
- `SHA256SUMS.txt`：发布资产完整性校验。

GitHub 版本使用固定发布证书进行 MSIX 签名。首次 one-click 安装会出现一次 Windows UAC 提示，仅用于把 UCAD 公钥证书加入 `LocalMachine\TrustedPeople`；随后由当前用户上下文完成 MSIX 安装。后续使用同一发布证书的更新通常无需再次导入证书。

## 三语支持

应用与仓库从 v0.1 起建立简体中文（zh-CN）、日本語（ja-JP）、English（en-US）三语资源基础。包清单、主要 UI 字符串、README 与 Release Notes 均纳入三语结构。

## 仓库结构

```text
src/UCAD.Core/          CAD 几何与文档核心
src/UCAD.App/           WinUI 3 / Win2D 应用与 MSIX 清单
tests/                  自动化测试
packaging/              GitHub 一键安装与发布校验
release/                发布 SSOT 元数据
docs/                   Release Notes 与架构文档
.github/workflows/       CI 与 GitHub Release 工作流
```

## 开发

需要 .NET 10 SDK、Windows 10/11 与 Visual Studio / Build Tools 的 Windows SDK。普通开发构建：

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
```

几何核心测试：

```powershell
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

## 文档

- [路线图](ROADMAP.md)
- [架构说明](docs/ARCHITECTURE.md)
- [发布流程](docs/RELEASE-PROCESS.md)
- [打包说明](packaging/README.md)
- [贡献指南](CONTRIBUTING.md)
- [支持](SUPPORT.md)
- [隐私](PRIVACY.md)
- [第三方说明](THIRD-PARTY-NOTICES.md)
- [v0.1.0 发布说明](docs/RELEASE-NOTES-v0.1.0.md)

## 许可证

UCAD 以 **GPL-2.0-only** 发布。第三方组件遵循各自许可证。
