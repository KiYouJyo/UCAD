# UCAD v0.3.5 — Workspace Shell Foundation

v0.3.5 将 Figma 中确认的 Fluent CAD 工作区正式接入现有 CAD Core。这个版本不扩张绘图命令数量，而是把 v0.3 已经完成的能力放进一套可以继续承载 v0.4/v0.5 的稳定桌面工作区。

## 新工作区

- 标题栏改为浏览器式多文档标签。
- 每个标签都是真实独立的内存 CAD 会话，拥有自己的 `CadDocument`、`CadViewport`、`CommandSession`、缩放/平移和命令上下文。
- 顶部采用“分类栏 + 持久工具棚”：点击“绘图”后完整绘图工具保持展开，再次点击才收起。
- 左侧保留高频工具栏，右侧保留 Inspector，底部为命令行和状态栏。
- 命令搜索直接读取 `CommandRegistry`，而不是维护第二套 UI 命令表。

## 已接入的现有能力

- LINE / L
- PLINE / PL
- RECTANGLE / REC
- CIRCLE / C
- ARC / A
- UNDO / U
- REDO
- CLEAR
- RESETVIEW / RV
- 鼠标与键盘坐标混合输入
- `x,y`、`@x,y`、距离输入
- Enter / Space 确认、Esc 取消、重复上一命令
- 自适应网格、十字光标、缩放和平移

## 为 v0.4 建立的 Core 接口

- `CadCommandDefinition` 新增 `CadCommandCategory` 与可选 `DrawingCommandKind`，UI 不再通过命令字符串判断工作流类型。
- `CadDocument` 新增 `Changed`、`Revision` 和结构化变更事件，标签、Inspector 与历史按钮可直接观察 Core 状态。
- `CadViewport` 支持注入外部 `CadDocument`，不再隐含“一个窗口只能有一个文档”。
- 新增 `CadWorkspaceSession`，明确一个文档任务所拥有的 Core、Viewport 与命令上下文。

这些接口将作为 v0.4 选择、OSNAP、Ortho 和属性 Inspector 的接入点。

## 有意保持禁用的项目

新 UI 已为选择、MOVE/COPY/OFFSET/TRIM、图层、HATCH、OSNAP、ORTHO 等能力预留位置，但 v0.3.5 不会伪装这些功能已经实现。对应控件保持可见但禁用，等待后续 Core 能力完成后直接启用。

## 多语言

新工作区完整覆盖：

- 简体中文（zh-CN）
- 日本語（ja-JP）
- English（en-US）

## 注意

v0.3.5 的多文档标签当前是内存工作区。文件保存/打开尚未实现，因此关闭包含绘图内容的标签时会明确提示内容将丢失。
