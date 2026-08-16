# UCAD v0.7.0 — CAD Authoring Foundation

本候选版把原计划的 v0.5、v0.6、v0.7 合并为一次完整验收：在 v0.4.1 选择、OSNAP、Ortho 与 CAD 光标基础上，同时完成修改、图层/对象属性、文字/标注/填充与图块工作流。

## v0.5：Modify

- MOVE (`M`)、COPY (`CO`/`CP`)、ROTATE (`RO`)、SCALE (`SC`)、MIRROR (`MI`)
- OFFSET (`O`)、TRIM (`TR`)、EXTEND (`EX`)
- 支持先选对象再执行命令，以及先执行命令、选择对象、Enter 确认两种 CAD 工作流
- 修改点输入复用现有 OSNAP；MOVE/COPY 支持 F8 Ortho；变换与 OFFSET 有实时预览
- 变换编辑保持实体 ID；COPY、保留源对象的 MIRROR、OFFSET 生成新 ID
- `CadDocument.Replace` / `ReplaceRange` 保证编辑进入统一 Undo/Redo 事务

## v0.6：Layers & Properties

- 文档级图层表与受保护的 `0` 图层
- 新对象自动继承当前图层
- 创建、重命名、删除、切换当前图层
- 图层可见性、锁定、颜色、线宽、线型元数据
- 对象级图层、颜色、线宽、线型覆盖；颜色/线宽支持 ByLayer
- 隐藏图层不绘制、不参与 OSNAP；锁定或隐藏图层不能被选择或 Modify 拾取
- `LAYER` / `LA` 图层管理器与 `CHPROP` / `CH` 对象属性编辑
- 图层与对象属性状态纳入文档 Undo/Redo 快照

## v0.7：Annotation, Hatch & Blocks

- `TEXT` / `T`：单行文字，指定插入点、文字高度与旋转角
- `DIM` / `DLI` / `DIMLINEAR`：基础对齐线性标注
- `HATCH` / `H`：对已选择的闭合 Polyline 或 Circle 创建 Solid 填充
- Text / Dimension / Hatch 进入统一渲染、选择几何、Grip 与 Modify 变换管线
- 文档级 Block Definition 表
- `BLOCK` / `B`：从当前选择建立可复用图块定义并指定基点
- `INSERT` / `I`：选择图块、比例与旋转角，再指定插入点
- `EXPLODE` / `X`：分解一个图块参照，并作为一次可 Undo 的 Replace 事务

## 验证

v0.7.0 要同时通过 Core tests、WinUI app build、startup-smoke、Interaction Smoke、Localization Smoke、Modify Smoke、Authoring Smoke 与 MSIX/one-click package validation。Authoring Smoke 在真实 UCAD 进程内验证 Layers + Properties + Text + Dimension + Hatch + Block + Insert + Explode；Modify Smoke 继续在真实进程内串行验证八条 v0.5 修改命令。

三语界面继续使用显式 MRT Core `ResourceContext`，简体中文 / 日本語 / English 可无重启切换；v0.4.1 的两点 Window/Crossing、Shift 减选、透明系统指针、Win2D CAD 光标、F3/F8 与多文档隔离均作为回归门槛保留。

## 当前边界

本版本仍是 CAD authoring foundation，而不是完整 AutoCAD 替代品。暂不包含 DXF 导入导出、打印/PDF、复杂标注样式、复杂 Hatch pattern/岛检测、动态图块/属性块、STRETCH/ARRAY/FILLET/CHAMFER、3D/BIM/DWG 完整兼容。上述后续能力从 v0.8 起继续推进。
