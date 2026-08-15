# UCAD v0.3.6 — Startup Reliability Hotfix

v0.3.6 是针对 v0.3.5 启动回归的可靠性热修。v0.3.5 引入真实多文档工作区后，首个 `CadWorkspaceSession` 会在 `MainWindow` 构造阶段立即创建，并随之提前实例化 Win2D `CadViewport`。在部分实际安装环境中，这条路径可能导致应用在窗口显示前退出，表现为点击 UCAD 后没有可见反应。

## 修复

- 将首个 CAD 工作区的创建从 `MainWindow` 构造阶段延迟到根视觉树 `Loaded` 后执行。
- 保留 v0.3.5 的多文档架构，不回退 `CadWorkspaceSession`、独立 `CadDocument`、`CadViewport` 或 `CommandSession`。
- 新增启动阶段诊断日志，记录窗口构造、激活、初始工作区创建以及未处理异常。
- 启动日志位置：`%LOCALAPPDATA%\UCAD\Logs\startup.log`。

## CI 防回归

此前 CI 只验证 Core 测试、WinUI 编译和 MSIX 打包，无法发现“可以编译和打包，但启动后立即退出”的运行时问题。

v0.3.6 新增 `startup-smoke`：

- 在 Windows runner 上实际启动 `UCAD.App.exe`；
- 等待 8 秒确认进程仍然存活；
- 若应用提前退出，自动输出 `startup.log` 并使 CI 失败。

同时 PR CI 继续要求以下项目全部通过：

- Core tests
- WinUI app build
- Startup smoke
- MSIX / one-click package validation

## 范围

此版本只处理启动可靠性和对应的验证基础设施，不改变 v0.3.5 已建立的工作区 UI、绘图命令和 CAD Core 边界。v0.4.0 的 Selection、OSNAP、Ortho 等交互能力仍按原路线推进。
