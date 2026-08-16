[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
面向城市规划与建筑设计的轻量二维 CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 当前定位

UCAD 目标是建立一套 Windows 原生、接近 AutoCAD 操作习惯、面向建筑与规划高频二维制图任务的轻量 CAD。坚持 DXF-first、2D-first，不以复制完整 AutoCAD 为目标。

**当前候选版本 v0.5.0 — Modify Foundation。** 本版本不继续扩张 UI，而是在 v0.4.x Selection / OSNAP / Ortho 基础上补齐第一组真正可用的修改命令：MOVE / COPY / ROTATE / SCALE / MIRROR / OFFSET / TRIM / EXTEND，并建立统一的几何变换、修改事务和实时预览输入链。

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

## CAD 交互

- 单击对象选择；继续单击其他对象可累加多选。
- Shift + 单击或 Shift + 框选可从当前选择集中移除对象。
- 空白处单击第一角后可松开鼠标，移动预览选框，再次单击提交；按住拖拽后松开的快速框选仍然保留。
- 左→右为 **Window**：只选择完全包含的对象；右→左为 **Crossing**：包含或相交的对象都会选择。
- 完成一个空的非 Shift 选框会清除当前选择；Esc 优先取消尚未完成的选框，再清除已完成选择。
- 绘图区隐藏 Windows 原生指针，仅保留 Win2D 绘制的十字准线 + 中央 Pickbox；十字准线 5–100%，Pickbox 3–20 px（默认 10 px），OSNAP aperture 3–50 px（默认 10 px），均可在“设置 → 输入与交互 → CAD 光标”实时调整。
- **F3 / 状态栏 OSNAP**：切换对象捕捉；当前基础集合为端点、中点、圆心、交点。
- **F8 / 状态栏 ORTHO**：切换 LINE / PLINE 以及适用 Modify 点位输入的水平/垂直约束。
- **Delete / ERASE / E / DELETE**：删除当前选择；多对象删除是一个 Undo 步骤。
- Inspector 会读取真实的 Line / Polyline / Circle / Arc 选择，显示类型、数量、基础几何与实体 ID。

## v0.5.0 Modify

- 支持**先选对象再执行命令**，也支持**先启动命令再完成选择并按 Enter**。
- MOVE / COPY：基点 + 第二点；画布点位支持 OSNAP，F8 Ortho 可约束位移方向。
- ROTATE：基点后可点击指定方向，或在命令行输入角度（度）。
- SCALE：基点后可输入正比例因子，或通过画布点位确定比例。
- MIRROR：两点定义镜像轴，默认保留源对象，也可选择删除源对象。
- OFFSET：输入距离 → 选择对象 → 指定偏移侧；基础支持 Line / Polyline / Circle / Arc。
- TRIM / EXTEND：采用快速模式，当前其他实体直接作为边界，可连续选择目标，Enter 结束。
- 变换和 OFFSET 使用瞬时预览；几何算法位于 Core，不依赖 WinUI 事件代码。
- 实际修改默认保留实体 ID，COPY / 保留源的 MIRROR / OFFSET 等新对象获得新 ID。
- MOVE / ROTATE / SCALE / 删除源的 MIRROR 使用 `ReplaceRange`，TRIM / EXTEND 使用 `Replace`，从而保持一次操作对应一次 Undo 事务。

## 当前命令

| 命令 | 别名 | 功能 |
| --- | --- | --- |
| `LINE` | `L` | 连续直线 |
| `PLINE` | `PL` | 多段线 |
| `RECTANGLE` | `REC` | 两角点矩形 |
| `CIRCLE` | `C` | 圆心/半径圆 |
| `ARC` | `A` | 三点圆弧 |
| `MOVE` | `M` | 移动选择对象 |
| `COPY` | `CO`, `CP` | 复制选择对象 |
| `ROTATE` | `RO` | 绕基点旋转 |
| `SCALE` | `SC` | 绕基点缩放 |
| `MIRROR` | `MI` | 两点轴镜像 |
| `OFFSET` | `O` | 按距离与侧点偏移 |
| `TRIM` | `TR` | 快速修剪 |
| `EXTEND` | `EX` | 快速延伸 |
| `ERASE` | `E`, `DELETE` | 删除当前选择 |
| `UNDO` | `U` | 撤销 |
| `REDO` | — | 重做 |
| `CLEAR` | — | 清空图形 |
| `RESETVIEW` | `RV` | 重置视图 |

所有命令入口统一路由到 `CommandRegistry → CommandSession → CAD Core`。Modify 分类已由占位状态转为真实能力；Annotate / Layer / Block / Measure 等尚无底层能力的分类继续保持不可用。

## Settings 与显示基线

- WinUI 3 / Windows App SDK 原生窗口，`PerMonitorV2` 高 DPI awareness；
- Figma 1440×900 Frame 继续作为视觉 SSOT，但当前功能里程碑不以像素级 UI 精校作为发布门槛；
- App Theme 与 CAD Canvas Theme 独立生效；
- 栅格显示/强度、光标中心缩放、中键平移、滚轮方向、选择预览和坐标格式进入运行时逻辑；
- “默认对象捕捉 / 默认捕捉类型 / 默认正交”会初始化之后新建的 Drawing；现有 Drawing 的 F3/F8 状态不会被默认设置强制覆盖；
- 尚无底层能力的 Restore Session、手动 UI Scale、自动更新、最近文件清理等不会显示成“已实现”；
- 设置集中由 `SettingsService` / `AppSettings` 管理，并保存到 `%LOCALAPPDATA%\UCAD\settings.json`。

## 三语支持

应用与仓库使用简体中文（zh-CN）、日本語（ja-JP）、English（en-US）三语资源。独立 MRT Core `ResourceContext` 支持**无需重启**切换语言，并立即刷新当前 Window、Start、Settings、文档标签、菜单、Inspector、命令区、Modify 阶段提示与状态栏；现有图形、选择会话、Undo/Redo 与视口状态不会因为语言切换被重建。

## 仓库结构

```text
src/UCAD.Core/          几何、实体、文档历史、命令、交互与 Modify 核心
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

CI 强制覆盖 Core tests、app-build、真实 startup-smoke、MSIX/package validation、三语资源契约、版本 SSOT、PerMonitorV2、Unicode 占位图标扫描与冻结的 Figma 关键 Design Tokens。Interaction Smoke 在真实 UCAD 内验证 Selection、ERASE、OSNAP、Ortho 与 Inspector；Localization Smoke 在同一进程依次切换 zh-CN → ja-JP → en-US；**Modify Smoke** 则在同一真实进程依次执行 MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND。

像素级 Figma 对照保留在手动 `UI Fidelity Screenshots` 工作流中，不阻塞功能开发或发布。

## 文档

- [路线图](ROADMAP.md)
- [架构说明](docs/ARCHITECTURE.md)
- [发布流程](docs/RELEASE-PROCESS.md)
- [打包说明](packaging/README.md)
- [v0.5.0 发布说明](docs/RELEASE-NOTES-v0.5.0.md)

## 许可证

UCAD 以 **GPL-2.0-only** 发布。