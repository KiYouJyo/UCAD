# UCAD v0.4.0 — Selection / OSNAP / Ortho Interaction Foundation

v0.4.0 将开发重点从界面精调转向 CAD 基础交互闭环。本版本不增加 Modify 类编辑命令，重点建立可复用的选择、对象捕捉、正交与属性检查边界，为 v0.5.0 的 MOVE / COPY / TRIM / OFFSET 等修改命令提供稳定基础。

## 主要更新

- 新增文档级 `SelectionSet`，选择状态不再由 XAML 控件临时持有，并会在实体被撤销/删除后自动清理失效选择。
- 支持空闲状态下单击选择、连续累加选择，以及 AutoCAD 式框选方向逻辑：左→右为窗口选择（完全包含），右→左为交叉选择（相交即选）。
- 新增选择预览高亮、已选对象高亮与 grip 点反馈；空白单击或 Esc 清除当前选择。
- 新增基础 OSNAP：端点、中点、交点；Core 同时预留圆/圆弧中心捕捉能力。捕捉孔径按屏幕像素换算为世界坐标，不随缩放失真。
- OSNAP 接入实际鼠标绘图输入与预览；F3 或状态栏 OSNAP 可即时切换，每个图纸会话保持独立状态。
- 新增 Ortho 正交约束并接入 LINE / PLINE 的鼠标绘图输入；F8 或状态栏 ORTHO 可即时切换，每个图纸会话保持独立状态。
- Settings 中“默认对象捕捉 / 默认捕捉类型 / 默认正交”现在会真正应用于之后新建的 Drawing 会话。
- Inspector 开始读取真实选中实体：支持 Line、Polyline、Circle、Arc 的类型、数量、基础几何摘要与实体 ID；命令生命周期变更也会同步更新 Inspector。
- 工具分类可用性开始由 `CommandRegistry` 的实际注册能力推导，未实现的 Modify / Annotate / Layer / Block / Measure 分类不会伪装成已有 Core 能力。
- 新增可复用的 `CadRect`、实体包围盒/距离/矩形相交、线段/圆/圆弧交点等 UI 无关几何查询，为后续 Modify 命令与空间索引继续复用。
- 保留 v0.3.10 的简体中文 / 日本語 / English 无重启热切换，新增 v0.4.0 交互文案三语资源。

## 操作

- 单击对象：选择对象；继续单击其他对象可累加选择。
- 空白单击或 Esc：清除选择。
- 左→右拖动：窗口选择，只选完全包含的对象。
- 右→左拖动：交叉选择，选择包含或与窗口相交的对象。
- F3：切换对象捕捉（OSNAP）。
- F8：切换正交模式（ORTHO）。

## 范围说明

v0.4.0 不包含 MOVE、COPY、ROTATE、TRIM、EXTEND、OFFSET 等修改命令，也不实现完整 AutoCAD OSNAP 类型全集。上述能力继续留给 v0.5.x；本版本先冻结选择与绘图辅助的 Core / Workspace / Viewport 边界。

## 验收

- Core tests 覆盖选择集合、点选命中、窗口/交叉选择、Line/Circle/Arc 命中、Endpoint/Midpoint/Intersection/Center snap、线圆/圆圆交点、Ortho 约束与 CommandSession 可观察生命周期。
- 保留 app-build、真实 startup-smoke、MSIX / one-click package validation、三语资源 key parity、版本 SSOT 与 PerMonitorV2 验证。
- UI 像素级精调不是本版本发布门槛；既有 Figma 关键 Design Tokens 仍受 CI 保护，不允许回归。
