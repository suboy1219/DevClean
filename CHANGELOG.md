# DevClean v1.0.0

首个正式版本：面向开发者的 Windows 磁盘清理工具。

- 扫描 Windows 系统、开发工具链、浏览器和常用应用缓存，按大小与风险分组展示。
- 提供清理确认、风险分级、管理员重启、逐项清理结果和本地日志。
- 支持命令行扫描、JSON 报告、清理自检和演示模式。
- 附带中英文使用文档和演示截图；当前图形界面为中文。

## 下载与运行

下载附件 `DevClean.exe`，在 Windows x64 上双击运行。单文件版本自带 .NET 10 运行时，无需安装 .NET。

运行 `DevClean.exe --demo` 可预览示例界面；`DevClean.exe --selftest` 可运行内置自检。

`SHA256SUMS.txt` 提供发布文件的 SHA-256 校验值。

程序尚未进行代码签名，首次启动可能出现 SmartScreen 提示。部分系统清理项需要管理员权限，使用中的文件会跳过。

## English

First stable release of DevClean, a Windows disk cleanup tool for developers.

- Scans Windows, developer toolchain, browser, and application caches with size and risk information.
- Includes cleanup confirmation, risk levels, administrator relaunch, per-item results, and local logs.
- Provides CLI scanning, JSON reports, a cleanup self-test, and demo mode.
- Includes Chinese and English documentation; the current GUI is Chinese.

Download `DevClean.exe` for Windows x64. This portable single-file build includes the .NET 10 runtime.
Use `--demo` to preview the UI and `--selftest` to run the built-in self-test.
Verify the download with `SHA256SUMS.txt`.

The executable is unsigned. Some system cleanup tasks require administrator rights; files in use are skipped.
