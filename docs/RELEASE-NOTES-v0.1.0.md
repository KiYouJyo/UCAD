# UCAD v0.1.0 — Foundation Release

UCAD v0.1.0 建立了面向城市规划与建筑设计的轻量二维 CAD 的首个可运行技术基础。

## 本版内容

- WinUI 3 原生 Windows 桌面壳与 Win2D GPU 加速绘图视口。
- 自适应网格、原点轴线、全窗口十字光标。
- 以鼠标位置为中心的滚轮缩放与中键平移。
- 世界坐标 / 屏幕坐标转换。
- 独立的 `UCAD.Core` 几何与文档层。
- 基础 `CadPoint`、`CadVector`、`LineEntity`、`CadDocument`。
- 两点式交互直线绘制。
- 简体中文、日本語、English 三语资源基础。
- GitHub Windows CI、MSIXBundle 打包、固定证书签名与自动化 Release 基础设施。

## 安装

普通用户请下载 `UCAD-v0.1.0-x64-one-click.zip`，解压后双击 `① 安装UCAD.cmd`。安装器会验证公钥证书、Release SHA-256 和 MSIX Authenticode 签名，然后安装 `UCAD_0.1.0.0_x64.msixbundle`。

首次使用 one-click 安装时会出现一次 Windows UAC 提示，仅用于把 UCAD 公钥发布证书加入 `LocalMachine\TrustedPeople`。后续使用同一证书签名的 UCAD 更新通常无需再次建立信任。

## 当前范围

这是基础版本，还不是完整生产级 CAD。DXF/DWG、AutoCAD 风格命令行、选择、OSNAP、修改工具、图层、标注与规划专属工作流将在后续里程碑逐步加入。

## 下一步

v0.2 将优先建立命令别名与 Enter / Esc / Space 驱动的 CAD 命令状态机。
