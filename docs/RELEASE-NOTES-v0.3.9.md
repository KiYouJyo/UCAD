# UCAD v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation

v0.3.9 是一次 UI Foundation Completion 更新。本版本不扩展 CAD Core，而是把 WinUI 3 Shell、Start、Settings 与页面切换逻辑收束到 UCAD 的 Figma 视觉 SSOT，并建立可继续承载 Selection / OSNAP / Inspector 的稳定界面基础。

## 主要更新

- 重建浏览器式标题栏：UCAD Brand → 连续 Document Tabs → `+` → 可拖动区域 → 原生窗口控制按钮。
- 引入 Drawing / Start / Settings 三种明确的 Workspace Page 类型。默认 `+` 打开 Start；关闭“新标签页显示开始页”后，`+` 会直接创建空白 Drawing。
- 完成 Start Center：新建、打开入口、最近使用空状态、Blank / Architecture / Urban Planning 模板信息架构与 Learn UCAD。未实现的文件、最近使用与专业模板业务不会伪装为可用。
- 完成 General / Appearance / Drafting / Input & Interaction / Files & Save / Language & Region / About UCAD 七个 Settings 页面。
- Settings 统一采用 Figma 节奏：228 px 导航、54 px 内容起点、940×72 卡片、35 / 12 / 8 / 30 px 垂直 rhythm；About 应用卡为 940×128。
- App Theme 与 CAD Canvas Theme 独立生效：App Theme 切换 Shell/原生控件的明暗调色板；Canvas Theme 单独控制图形、临时预览、栅格和十字光标，并保持 Canvas Background 独立。
- 画布背景、栅格显示/强度、光标中心缩放、中键平移、滚轮方向、坐标精度和小数格式已进入实际运行逻辑。
- 对尚无底层实现的 Restore Session、手动 UI Scale、自动更新、最近文件清理等入口进行禁用或明确占位，不制造虚假的业务能力。
- 通用操作统一改用 Fluent / WinUI 图标；CAD 专业图标使用 PathIcon，清理 Unicode 占位符图标。
- 新增集中式 `SettingsService` / `AppSettings`，设置保存在 `%LOCALAPPDATA%\UCAD\settings.json`，避免在页面 code-behind 中散落存储键。
- 新增 zh-CN / ja-JP / en-US 完整 Start 与 Settings 资源；显示语言偏好在下一次 Shell 创建前统一应用，避免当前会话局部语言混用。
- 版本信息统一由根目录 `VERSION` 驱动，Assembly / UI / release metadata / MSIX Package 均对齐 0.3.9。
- 保持 PerMonitorV2，高 DPI 继续使用 XAML DIP，不引入 bitmap UI scaling。

## CAD Core

本版本不增加 Selection、OSNAP、Ortho、MOVE、COPY、TRIM、OFFSET、DWG/DXF、GIS、Architecture Objects 或 Planning Objects。

现有 LINE / PLINE / RECTANGLE / CIRCLE / ARC / Undo / Redo / Clear / Reset View、多文档会话、命令行、CommandRegistry、Zoom / Pan 继续通过现有 `CommandRegistry → CommandSession → CAD Core` 路径工作。

## 验收

必需 CI 覆盖：

- Core tests；
- app-build；
- startup-smoke，真实启动并初始化 Start / Settings；
- package-validation 与 one-click package 校验；
- Figma 关键尺寸、颜色与 Design Token 契约；
- Start / Settings / Canvas 关键行为契约；
- 三语言资源 key 一致性；
- PerMonitorV2；
- 版本 SSOT；
- Unicode 假图标扫描。

像素级 1440×900 Figma 对照保留为手动 `UI Fidelity Screenshots` 工作流。只有运行器提供真实的 1440×900 交互桌面时才执行，因此宿主桌面限制不会阻塞功能验收或正式发布。