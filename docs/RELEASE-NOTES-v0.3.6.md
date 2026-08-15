# UCAD v0.3.6 — Startup Reliability Hotfix

v0.3.6 是针对 v0.3.5 启动回归的可靠性热修。新增的真实启动烟雾测试与启动日志已经定位到确切根因：新 UI 在 `MainWindow` 构造阶段用 `ResourceLoader.GetString()` 读取了一个用于 XAML `x:Uid` 属性赋值的 `.Text` 资源键，运行时抛出 `0x80073B17 NamedResource Not Found`，导致应用在窗口显示前退出，因此表现为点击 UCAD 后没有可见反应。

## 修复

- 将工具栏提示的代码侧资源读取改为独立的普通命名资源 `ToolShelfHint`，不再把 `ToolShelfHintText.Text` 这样的 XAML 属性资源键传给 `ResourceLoader.GetString()`。
- `GetString()` 对缺失的运行时命名资源增加保护与诊断：单个本地化键缺失不再直接导致整个应用启动崩溃，并会记录到启动日志。
- 将首个 CAD 工作区的创建从 `MainWindow` 构造阶段延迟到根视觉树 `Loaded` 后执行，进一步收敛启动阶段的 Win2D/工作区初始化风险。
- 保留 v0.3.5 的多文档架构，不回退 `CadWorkspaceSession`、独立 `CadDocument`、`CadViewport` 或 `CommandSession`。
- 新增启动阶段诊断日志，记录窗口构造、激活、初始工作区创建以及未处理异常。
- 启动日志位置：`%LOCALAPPDATA%\UCAD\Logs\startup.log`。

## CI 防回归

此前 CI 只验证 Core 测试、WinUI 编译和 MSIX 打包，无法发现“可以编译和打包，但启动后立即退出”的运行时问题。

v0.3.6 新增并强化 `startup-smoke`：

- 在 Windows runner 上实际启动 `UCAD.App.exe`；
- 等待 8 秒确认进程仍然存活；
- 若应用提前退出，自动输出 `startup.log` 并使 CI 失败；
- 检查代码侧 `GetString()` 只能使用普通命名资源，禁止再次误用 `.Text` / `.Content` 等 `x:Uid` 属性键；
- 校验所有运行时资源键在 zh-CN、ja-JP、en-US 三套 RESW 中均存在；
- 即使应用没有退出，只要启动日志记录到 `MissingResource` 也会判定 CI 失败。

同时 PR CI 继续要求以下项目全部通过：

- Core tests
- WinUI app build
- Startup smoke / runtime localization contract
- MSIX / one-click package validation

PR 还会生成一份临时 one-click 验收包，用于在真实安装环境完成最终启动验收后再合并发布。

## 范围

此版本只处理启动可靠性和对应的验证基础设施，不改变 v0.3.5 已建立的工作区 UI、绘图命令和 CAD Core 边界。v0.4.0 的 Selection、OSNAP、Ortho 等交互能力仍按原路线推进。
