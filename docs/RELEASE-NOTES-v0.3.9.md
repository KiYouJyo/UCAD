# UCAD v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation

v0.3.9 是一次 UI Foundation Completion 更新。本版本不扩展 CAD Core，而是把 WinUI 3 实际界面收束到 UCAD Figma 视觉 SSOT，并建立可继续承载 Selection / OSNAP / Inspector 的稳定 Shell。

## 主要更新

- 重建浏览器式标题栏：UCAD Brand → 连续 Document Tabs → `+` → 可拖动区域 → 原生窗口控制按钮。
- 引入 Drawing / Start / Settings 三种明确的 Workspace Page 类型；`+` 打开 Start，只有从 Start 选择“新建图纸”才创建 `CadWorkspaceSession`。
- 完成 Start Center：新建、打开入口、最近使用空状态、Blank / Architecture / Urban Planning 模板信息架构与 Learn UCAD 区域；未实现的文件与模板业务不会伪装为可用。
- 完成 General / Appearance / Drafting / Input & Interaction / Files & Save / Language & Region / About UCAD 七个 Settings 页面。
- Settings 统一采用 Figma 节奏：228 px 导航、54 px 内容起点、940×72 卡片、35 / 12 / 8 / 30 px 垂直 rhythm；About 应用卡为 940×128。
- 应用主题与 CAD Canvas 设置保持独立；画布背景、栅格显示/强度、光标中心缩放、中键平移与滚轮方向已接入现有 Viewport 行为。
- 通用操作统一改用 Fluent / WinUI 图标；CAD 专业图标使用 PathIcon，清理 Unicode 占位符图标。
- 新增集中式 `SettingsService` / `AppSettings`，设置保存在 `%LOCALAPPDATA%\UCAD\settings.json`，避免在页面 code-behind 中散落 LocalSettings 键。
- 新增 zh-CN / ja-JP / en-US 完整 Start 与 Settings 资源；显示语言偏好在下一次启动前应用，避免当前会话局部混语。
- 版本信息统一由根目录 `VERSION` 驱动，Assembly / UI / release metadata / MSIX Package 均对齐 0.3.9。
- 保持 PerMonitorV2，高 DPI 继续使用 XAML DIP，不引入 bitmap UI scaling。

## CAD Core

本版本不增加 Selection、OSNAP、Ortho、MOVE、COPY、TRIM、OFFSET、DWG/DXF、GIS、Architecture Objects 或 Planning Objects。

现有 LINE / PLINE / RECTANGLE / CIRCLE / ARC / Undo / Redo / Clear / Reset View、多文档会话、命令行、CommandRegistry、Zoom / Pan 继续通过现有 `CommandRegistry → CommandSession → CAD Core` 路径工作。

## 验收

CI 继续覆盖 Core tests、app-build、startup-smoke 与 package-validation，并新增：

- Figma 关键尺寸与 Design Token 契约；
- 三语言资源 key 一致性；
- PerMonitorV2 契约；
- 版本 SSOT 一致性；
- Unicode 假图标扫描；
- Drawing / Start / Settings General / Appearance / Input & Interaction / About 的 1440×900 实际运行截图产物。
