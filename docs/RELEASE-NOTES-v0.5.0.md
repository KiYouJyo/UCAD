# UCAD v0.5.0 — Modify Foundation

v0.5.0 在 v0.4.x 的 Selection / OSNAP / Ortho 基础上补齐第一组真正可用的 CAD 修改命令。重点不是扩张 UI，而是建立统一的几何变换、修改事务和交互输入链，使修改命令与既有 `CommandRegistry → CommandSession → CadWorkspaceSession → CadDocument` 架构保持一致。

## 主要更新

- **MOVE / M**：支持预选或命令后选择，指定基点与第二点完成移动；保留实体 ID，因此选择状态可连续保持；一次操作对应一个 Undo。
- **COPY / CO / CP**：按基点与第二点创建新实体副本，副本使用新的实体 ID。
- **ROTATE / RO**：指定基点后可通过画布点位或命令行角度（度）旋转选中对象，并提供瞬时预览。
- **SCALE / SC**：指定基点后可输入正比例因子或通过点位确定比例，支持实时预览。
- **MIRROR / MI**：通过两点定义镜像轴；默认保留源对象，也可选择删除源对象。
- **OFFSET / O**：输入偏移距离、选择对象并指定偏移侧。基础实现覆盖 Line、Polyline、Circle、Arc。
- **TRIM / TR**：采用快速修剪逻辑，当前其他可见实体均可作为边界；点击目标段修剪，可连续处理多个目标，Enter 结束。
- **EXTEND / EX**：采用快速延伸逻辑，将选中的 Line、开放 Polyline 或 Arc 端部延伸到当前方向上最近的有效边界，可连续处理多个目标。

## 统一修改底座

- 新增共享的不可变几何变换层 `CadEntityTransform`，统一处理平移、旋转、缩放与镜像。
- 新增 `CadOffset` 和 `CadTrimExtend`，将几何计算保持在 Core 层，不把算法塞入 WinUI 事件代码。
- `CadDocument` 新增 `Replace` / `ReplaceRange` 一步事务：MOVE / ROTATE / SCALE / MIRROR 以及 TRIM/EXTEND 均可作为单次 Undo 恢复。
- 对实际修改默认保留实体 ID；COPY、保留源对象的 MIRROR、OFFSET 等新生成实体使用新 ID。
- Modify 交互继续复用 v0.4.x 的 SelectionSet、OSNAP、Ortho 和透明系统光标 + Win2D CAD 光标体系。
- 支持命令前预选与命令后选择两种 CAD 常见工作流。

## 交互与界面

- Modify 分类从占位状态转为真实可用，并将八条基础修改命令接入统一命令注册表。
- MOVE / COPY 等点位型命令支持对象捕捉；MOVE / COPY 的位移点继续支持 F8 Ortho。
- 变换命令和 OFFSET 提供画布瞬时预览。
- 新增修改阶段提示，并继续支持简体中文 / 日本語 / English 无重启即时切换。
- v0.4.1 已验收的两点式 Window/Crossing、Shift 减选、可调 Crosshair/Pickbox/OSNAP aperture 不回退。

## 验证

- Core 测试覆盖实体 ID 保留、一步 Undo、平移/旋转/缩放/镜像、Line/Polyline/Circle/Arc 偏移以及 Quick TRIM/EXTEND 的代表性几何情况。
- 新增独立 **Modify Smoke**：启动真实 UCAD 进程，并在同一进程中依次执行 MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND，必须写出成功标记才算通过。
- 保留 Core tests、app-build、startup-smoke、Interaction Smoke、Localization Smoke、MSIX/one-click package validation、PerMonitorV2、版本 SSOT 与三语资源一致性检查。

## 范围说明

v0.5.0 是第一阶段 Modify Foundation。高级倒角/圆角、阵列、拉伸、夹点编辑、复杂对象与 DWG 兼容仍不在本版本范围内；这些功能将在后续版本基于本次建立的统一修改事务和几何服务继续扩展。
