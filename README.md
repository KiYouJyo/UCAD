[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
面向城市规划与建筑设计的轻量二维 CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 当前定位

UCAD 目标是建立一套 Windows 原生、接近 AutoCAD 操作习惯、面向建筑与规划高频二维制图任务的轻量 CAD。坚持 **2D-first / DXF-first**，不以复制完整 AutoCAD 为目标。

**当前验收候选版本：v0.7.0 — CAD Authoring Foundation。** 该候选版把 v0.5 Modify、v0.6 Layers & Properties、v0.7 Annotation / Hatch / Blocks 合并为一次验收，在 v0.4.1 的选择、OSNAP、Ortho 与 CAD 光标基础上完成第一套连续的“绘制 → 选择 → 修改 → 分层 → 标注 → 复用”工作流。

## 安装

正式版本从 [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) 下载：

- `UCAD-v<version>-x64-one-click.zip`：普通用户推荐；
- `UCAD_<packageVersion>_x64.msixbundle`：高级用户直接侧载；
- `SHA256SUMS.txt`：完整性校验。

PR 验收阶段会生成与正式发布证书一致的 release-signed acceptance package，用于合并前实机验证。

## 工作区

UCAD 保留三种标签内容：Drawing、Start、Settings。每个 Drawing 都有独立的 `CadDocument`、`CadInteractionState`、`CadViewport` 与 `CommandSession`，因此图形、Undo/Redo、选择、OSNAP、Ortho、图层、命令与视口状态彼此隔离。

v0.4.1 的 CAD 交互继续作为基线：两点或拖拽 Window/Crossing、PICKADD 式累加选择、Shift 减选、透明 Windows 指针 + Win2D 十字准线/Pickbox、F3 OSNAP、F8 Ortho、Delete/ERASE 与实时 Inspector。

## v0.5 — Modify

- **MOVE / M**：基点 → 第二点；支持 OSNAP / F8 Ortho / 实时预览；
- **COPY / CO / CP**：生成独立新实体；
- **ROTATE / RO**：鼠标方向或数字角度；
- **SCALE / SC**：鼠标或数字比例；
- **MIRROR / MI**：两点镜像轴，可保留或删除源对象；
- **OFFSET / O**：距离 → 对象 → 方向侧；基础覆盖 Line / Polyline / Circle / Arc；
- **TRIM / TR**：Quick Trim，其他可见图形作为边界，可连续修剪；
- **EXTEND / EX**：向最近有效边界连续延伸。

支持“先选对象 → 命令”和“命令 → 选择 → Enter”两种 CAD 顺序。编辑使用统一的 `CadEntityTransform`、`CadOffset`、`CadTrimExtend` 与 `CadDocument.Replace/ReplaceRange`，每次提交进入统一 Undo/Redo 历史。

## v0.6 — Layers & Properties

- 文档级图层表，内置并保护 `0` 图层；
- 当前图层决定新建对象的默认归属；
- 创建、重命名、删除、切换当前图层；
- 图层可见性、锁定、颜色、线宽、线型元数据；
- 对象可覆盖图层、颜色、线宽、线型，颜色/线宽支持 **ByLayer**；
- 隐藏图层不绘制、不参与 OSNAP；隐藏/锁定图层不参与选择与 Modify 拾取；
- `LAYER / LA` 打开图层管理器；`CHPROP / CH` 修改当前选择属性；
- 图层表、当前图层与对象属性也纳入文档 Undo/Redo。

## v0.7 — Annotation, Hatch & Blocks

- **TEXT / T**：单行文字，插入点、高度、旋转角；
- **DIM / DLI / DIMLINEAR**：基础对齐线性标注；
- **HATCH / H**：为已选择的一条闭合 Polyline 或 Circle 创建 Solid 填充；
- **BLOCK / B**：把当前选择定义为可复用图块并指定基点；
- **INSERT / I**：选择图块、比例、旋转角并指定插入点；
- **EXPLODE / X**：分解一个 Block Reference，并作为一次 Undoable Replace 事务。

Text / Dimension / Hatch / Block Reference 已进入统一渲染、选择几何、Grip、OSNAP/相交查询与 Modify transform 基础管线。

## 当前命令

| 类别 | 命令 |
| --- | --- |
| Draw | `LINE (L)`, `PLINE (PL)`, `RECTANGLE (REC)`, `CIRCLE (C)`, `ARC (A)`, `HATCH (H)` |
| Modify | `MOVE (M)`, `COPY (CO/CP)`, `ROTATE (RO)`, `SCALE (SC)`, `MIRROR (MI)`, `OFFSET (O)`, `TRIM (TR)`, `EXTEND (EX)`, `EXPLODE (X)` |
| Annotate | `TEXT (T)`, `DIM (DLI/DIMLINEAR)` |
| Layers / Properties | `LAYER (LA)`, `CHPROP (CH)` |
| Blocks | `BLOCK (B)`, `INSERT (I)` |
| Edit / View | `ERASE (E/DELETE)`, `UNDO (U)`, `REDO`, `CLEAR`, `RESETVIEW (RV)` |

所有命令仍统一经过 `CommandRegistry → CommandSession → CadWorkspaceSession / CadDocument`，不建立第二套命令、选择或历史系统。

## 三语与显示基线

应用使用简体中文（zh-CN）、日本語（ja-JP）、English（en-US）三语资源。显式 MRT Core `ResourceContext` 支持**无需重启**切换当前 Window、Start、Settings、Drawing shell 与新增 authoring dialogs/prompts；现有图形、选择、Undo/Redo、图层与视口状态不会被重建。

WinUI 3 / Windows App SDK 保持 `PerMonitorV2`；Figma 1440×900 Frame 与关键 Design Tokens 继续作为 shell 视觉 SSOT，但 v0.5–v0.7 的发布门槛优先保证 CAD 功能与交互正确性，不重新开启像素级 UI 大改。

## 验证

必需 CI 包括 Core tests、app-build、startup-smoke、Interaction Smoke、Localization Smoke、Modify Smoke、Authoring Smoke、MSIX / one-click package validation、版本 SSOT、PerMonitorV2、透明 CAD cursor 与三语资源 parity。

其中 Modify Smoke 在一个真实 UCAD 进程内依次执行八条 v0.5 修改命令；Authoring Smoke 在真实进程内验证 Layers + Properties + Text + Dimension + Hatch + Block + Insert + Explode。

## 后续

v0.8 起继续推进 DXF-first import/export、print/PDF、建筑辅助、规划地块/指标、GIS exchange 与大型图纸性能回归。1.x 明确不以 3D/BIM、渲染、点云、完整 DWG/AutoCAD 插件兼容为目标。

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
- [v0.7.0 发布说明](docs/RELEASE-NOTES-v0.7.0.md)

## 许可证

UCAD 以 **GPL-2.0-only** 发布。
