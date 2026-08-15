# UCAD v0.3.0 — Drawing Foundation

v0.3 是 UCAD 第一版真正形成“输入命令 → 几何预览 → 实体落图 → 撤销/重做”的二维绘图闭环。

## 五套绘图命令

- `LINE` / `L`：连续两点直线；每个线段形成独立 Line 实体。
- `PLINE` / `PL`：连续指定顶点，Enter / Space 后作为一个 Polyline 实体提交。
- `RECTANGLE` / `REC`：两个对角点生成闭合 Polyline 矩形。
- `CIRCLE` / `C`：圆心 + 半径点；指定圆心后也可直接键入半径。
- `ARC` / `A`：起点 + 圆弧上第二点 + 终点的三点定弧。

全部命令都可以混用鼠标点取与 v0.2 的 `x,y`、`@x,y`、距离输入，并带蓝色实时预览。新建画布不再自动放置演示线。

## Undo / Redo

- `UNDO` / `U` 撤销上一项已提交绘图操作。
- `REDO` 重做上一项撤销操作。
- 工具栏 Undo / Redo 会按文档历史自动启停。
- CLEAR 同样进入可撤销历史。

Undo/Redo 位于 `UCAD.Core` 文档层，为后续 MOVE、COPY、TRIM、OFFSET 等修改命令保留统一基础。

## 几何模型

新增 `PolylineEntity`、`CircleEntity` 和 `ArcEntity`。Arc 由三点计算真实圆心、半径与扫角，显示时才进行采样，不把折线近似当成模型数据。

## 安装

推荐下载 `UCAD-v0.3.0-x64-one-click.zip`，解压后运行 `① 安装UCAD.cmd`。已有 UCAD 发布证书信任的设备通常无需再次导入证书。

## 下一步

v0.4 将进入交互基础：单击/窗口/交叉选择、多选、Delete、Endpoint/Midpoint/Center/Intersection OSNAP 与 Ortho。
