# UCAD v0.2.0 — Command Foundation

v0.2 把 UCAD 从“可以画一条线的技术预览”推进为真正具有 CAD 命令交互骨架的应用。

## 本版新增

- 新增底部命令行，可直接键入命令。
- 新增命令注册表与别名系统；当前 `LINE` 支持 AutoCAD 常用别名 `L`。
- Enter 与 Space 均可确认输入。
- Esc 可取消当前命令。
- 空输入时 Enter / Space 可重复上一条命令。
- 支持绝对坐标 `x,y`。
- 支持相对坐标 `@x,y`。
- 已有基点时可只输入距离，并沿当前光标方向生成下一点。
- 鼠标点取与命令行坐标输入统一进入同一 LINE 点提交路径。
- `CLEAR` 与 `RESETVIEW` 也进入统一命令注册表。
- Core 自动化测试覆盖别名解析、命令生命周期和坐标解析。

## 操作示例

```text
命令: L
LINE：指定第一点
0,0
LINE：指定下一点
@5000,0
@0,3600
[Enter]
```

也可以输入 `LINE` 后直接用鼠标连续点取，最后按 Enter、Space 或 Esc 结束。

## 安装

推荐下载 `UCAD-v0.2.0-x64-one-click.zip`，解压后运行 `① 安装UCAD.cmd`。首次建立发布证书信任时会出现一次 UAC；后续仍由普通用户上下文安装签名 MSIX。

## 下一步

v0.3 将在这套命令状态机上加入 LINE、PLINE、RECTANGLE、CIRCLE、ARC 的正式实体闭环，以及 Undo / Redo。
