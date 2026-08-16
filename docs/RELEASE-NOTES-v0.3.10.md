# UCAD v0.3.10 — Live Trilingual Localization Hotfix

v0.3.10 修复 v0.3.9 中 Start / Settings 三语资源未正确加载，以及显示语言必须重启才能生效的问题。本版本不增加 CAD Core 功能。

## 主要修复

- 修复 Start / Settings 显示 `Start_TabTitle`、`Settings_Nav_Title` 等资源键而非真实文字的问题。
- 按 Windows App SDK 的命名资源映射方式加载 `UcadV039.resw`，使用默认 PRI 路径 + `UcadV039` ResourceMap。
- 新增集中式 `LocalizationService`，统一管理 zh-CN / ja-JP / en-US 的运行时资源上下文。
- 支持**无需重启**切换简体中文、日本語、English；切换后立即刷新当前窗口、Start、Settings、文档标签、菜单、分类栏、Inspector、命令区与状态栏。
- 语言切换不重建 Window，不销毁现有 `CadWorkspaceSession`，因此当前图形、Undo/Redo 历史、视口状态与多文档会话保持不变。
- “跟随系统语言”继续受支持；关闭后可选择 zh-CN / ja-JP / en-US，并即时应用。
- 新建图纸的未命名标签会随语言切换更新，例如“图纸 1 / 図面 1 / Drawing 1”。
- 更新语言设置说明，不再提示必须重启 UCAD。

## 验收

新增独立 Localization Smoke：在**同一个 UCAD 进程**内依次切换 `zh-CN → ja-JP → en-US`，并验证 Start、Settings 与 Shell 的代表性资源均返回真实翻译而不是资源键。

原有 Core tests、app-build、startup-smoke、package-validation、版本 SSOT、PerMonitorV2 与三语资源 key parity 继续保留。
