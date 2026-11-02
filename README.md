# ChinaClock · 北京时间任务栏时钟

一个极小的 Windows 桌面工具：把**北京时间 + 日期 + 农历黄历**贴在任务栏上常驻显示，
并在系统托盘提供菜单（显示 / 复制时间 / 今日黄历 / 开机自启 / 退出）。

- 完全**离线**运行，无任何网络请求、无遥测、不采集任何个人信息。
- 单文件 `ChinaClock.exe`（约 430 KB，已内嵌农历库），下载即用。
- 空闲 CPU ≈ 0%，常驻内存约 25 MB。

## 功能

- 任务栏左侧常驻显示 `北京时间 HH:mm:ss` 与 `日期 星期`。
- 第二行显示农历：`农历 月·日 · 当前节气 · 干支生肖年`。
- 托盘菜单：
  - 显示北京时间
  - 固定在任务栏最左侧
  - 今日黄历（农历 / 节气 / 宜忌 / 冲煞 等完整内容）
  - 复制北京时间（仅在点击时写入剪贴板）
  - 开机自动启动（开关，写入当前用户注册表 Run 项）
  - 退出
- 跟随系统深/浅色主题自动换色。

## 系统要求

- Windows 10 / 11（在 Windows 11 任务栏上效果最佳，见「已知限制」）
- .NET Framework 4.8（Win10/11 一般自带）

## 使用

直接运行 `ChinaClock.exe` 即可。程序为**单实例**：再次启动会静默退出（已在运行）。
想要开机自启，右键托盘图标 → 勾选「开机自动启动」。

## 构建

仓库不含编译产物，用 `build.bat` 一键构建（需要本机装有 Visual Studio 2022 或 Build Tools，脚本会自动定位 Roslyn `csc.exe`）：

```bat
build.bat
```

产物为单文件 `ChinaClock.exe`（`lunar.dll` 已作为资源内嵌，运行时自动加载，无需随附）。
构建脚本最后会自动运行 14 项测试（日期跨日/跨年、时区无关、任务栏定位、农历闰月与节气、文字宽度）。

手动编译（等价命令）：

```bat
csc /target:winexe /out:ChinaClock.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:lunar.dll /r:netstandard.dll ^
  /res:lunar.dll,ChinaClock.embedded.lunar.dll ^
  ChinaClock.cs
```

## 隐私与安全声明

- **无网络**：源码不使用任何 socket / HTTP / DNS，纯本地计算。
- **注册表**：仅两处——只读 `Themes\Personalize` 判断深浅色；仅在用户手动开启时写
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 的一条自身启动项（可用托盘菜单随时关闭）。
- **剪贴板**：仅在用户点击「复制北京时间」时写入时间字符串。
- 不读取、不上传任何个人数据。

## 已知限制

- 时钟固定在任务栏**最左侧**。Windows 11 任务栏图标居中，左侧为空白，显示正常；
  在 **Windows 10** 上「开始」按钮位于左下角，可能与该时钟重叠。
- 仅处理主显示器任务栏（`Shell_TrayWnd`），不处理副屏任务栏。
- DPI 采用系统级感知（v1），跨不同缩放的多显示器时可能轻微发虚。
- 为稳健起见，内部对定时器回调解开了异常吞掉；若某功能持续异常，不会崩溃但也无日志。

## 第三方依赖

- [lunar-csharp](https://github.com/6tail/lunar-csharp) 1.6.8（作者 6tail，MIT 许可），
  用于农历 / 节气 / 干支 / 黄历计算。许可证见 [`lunar.LICENSE`](./lunar.LICENSE)。

## 许可

本项目采用 MIT 许可，详见 [`LICENSE`](./LICENSE)。发布前请把 `LICENSE` 中的版权人
`ChinaClock contributors` 替换为你的名字或 GitHub 用户名。
