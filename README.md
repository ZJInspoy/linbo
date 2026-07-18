<p align="center">
  <img src="docs/linbo3-hero.png" alt="泠波 3.0" width="760">
</p>

<h1 align="center">泠波</h1>

<p align="center">面向 AIGC 工作流的 Windows 原生无限画布提示词管理工具</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows-111111">
  <img alt="UI" src="https://img.shields.io/badge/UI-native%20WPF-111111">
  <img alt="License" src="https://img.shields.io/badge/license-GPL--3.0-111111">
</p>

泠波把提示词、参考资料和临时图片组织在一张本地无限画布中。卡片只展示标题，正文作为隐藏内容保存，需要时可以一键复制。所有持久数据默认保存在本机，不依赖账号或云服务。

Linbo is a native Windows infinite-canvas prompt clipboard for AIGC workflows.

## 功能

- 无限画布：平移、缩放、小地图、框选建卡和双列瀑布流整理。
- 提示词卡片：标题、隐藏正文、一键复制、标签、置顶、归档和本地持久化。
- 文档导入：拖入 Word 或 TXT，以文件名作为标题、正文作为隐藏内容。
- 归档画布：翻面切换、恢复、单卡删除和清空归档。
- 临时参考画布：粘贴或拖入图片、等比缩放、涂鸦、镜像卡片和另存为。
- 窗口工作流：无边框窗口、五条边界调整、两级最大化和位置记忆。

## 下载

Windows 安装程序通过 [GitHub Releases](../../releases/latest) 发布。源码仓库不提交编译后的 EXE。

## 本地数据

泠波不会主动上传卡片内容。默认数据文件位于：

```text
%APPDATA%\Linbo\linbo-native-state.json
```

覆盖安装会保留本地数据。卸载时可以先将卡片反向导出为 Word，再清除本地数据。重要内容仍建议定期备份。

## 从源码构建

环境要求：

- Windows 10 或 Windows 11
- PowerShell 5.1 或更高版本
- .NET Framework 4.8 Developer Pack（推荐）

在仓库根目录执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果位于 `artifacts/`：

```text
Linbo.exe
LinboUninstall.exe
Linbo-3.0-Setup.exe
SHA256SUMS.txt
```

## 验证

构建完成后，以 STA 模式运行三个轻量验证脚本：

```powershell
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\tests\clipboard_probe.ps1
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\tests\scratch_visual_probe.ps1
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\tests\window_geometry_probe.ps1
```

## 参与贡献

欢迎通过 Issue 报告可复现的问题，也欢迎提交范围清晰的 Pull Request。涉及交互变化时，请说明操作路径、预期结果和实际结果；涉及数据读写时，请同时说明升级和异常退出场景。

## 许可证

泠波的代码和仓库内图像素材均按 [GNU General Public License v3.0](LICENSE) 发布。你可以使用、研究、修改和分发本项目；分发修改版本时，需要继续提供对应源码并保留相同许可证。
