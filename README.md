# 桌面文件夹 · Glass Folders

![成品预览](E:\code\GlassFolders\assets\效果预览.png)

Glass Folders 是一个 Windows 11 桌面组件，可以把常用文件夹以磨砂玻璃面板的形式放在桌面上。每个面板绑定一个实际目录，可浏览其中的文件和子文件夹。

## 下载与安装

**当前版本** `v1.0`  
**运行环境** Windows 11 x64，使用 .NET Framework 和 WPF。

发布后，可从仓库的 **Releases** 页面在 `v1.0` 下选择安装包或直接运行版。

| 文件 | 用途 |
| --- | --- |
| `GlassFolders-Setup-1.0.exe` | 为当前用户安装，提供开始菜单快捷方式、可选的登录时启动和卸载入口 |
| `GlassFolders.exe` | 直接运行，首次启动选择要绑定的文件夹 |
| `SHA256SUMS.txt` | 下载文件的 SHA-256 校验值 |

覆盖安装前，先从系统托盘退出正在运行的旧版。组件配置保存在当前用户的应用数据目录。若为直接运行的版本启用了开机启动，移动程序后，在托盘菜单中关闭再开启开机启动，以更新程序路径。现有发布文件为未签名构建。

## 操作

| 操作 | 效果 |
| --- | --- |
| 点击「＋」或使用托盘菜单 | 新建面板并绑定文件夹 |
| 拖动标题栏或边框 | 移动面板或调整大小，网格自动适应 |
| 点击文件、快捷方式或子文件夹 | 用系统默认程序打开文件，或在面板内进入子文件夹 |
| 点击「展开」 | 放大面板，显示搜索、滚动和返回上一级。搜索范围为当前目录 |
| 按 Esc 或点击面板外 | 收起面板并返回绑定的根目录 |
| 点击「⋯」 | 按名称、类型、修改时间或自定义顺序排列，也可以置顶、刷新或打开资源管理器 |
| 将图标拖到另一图标前 | 设置面板内的显示顺序。清空搜索框后可以继续拖动排序 |
| 点击「×」并确认 | 仅移除面板，保留磁盘上的文件 |

右键单个项目可在资源管理器中定位。图标大小固定，面板尺寸决定网格的行列数。程序默认隐藏系统文件和隐藏文件。托盘菜单还可以找回所有组件、切换开机启动或退出应用。

## 本地配置与卸载

| 路径 | 内容 |
| --- | --- |
| `%LOCALAPPDATA%\GlassFolders\settings.json` | 绑定的目录、面板位置和排序 |
| `%LOCALAPPDATA%\GlassFolders\settings.json.bak` | 配置更新时保留的备份 |
| `%LOCALAPPDATA%\GlassFolders\error.log` | 发生错误时写入的日志 |
| `%LOCALAPPDATA%\Programs\GlassFolders\` | 默认安装目录 |

读取配置失败时，程序会尝试保存一份带时间戳的 `settings.json.invalid-*` 副本。可从 Windows「已安装的应用」卸载。卸载会移除程序、快捷方式和开机启动项，并保留配置与绑定的文件。安装目录中可能保留卸载程序本身。

## 从源码构建

在 Windows PowerShell 中进入仓库根目录并运行以下命令。命令中的执行策略只适用于本次启动的 PowerShell 进程。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

脚本使用系统中的 64 位 .NET Framework C# 编译器与 WPF 程序集，无需下载 NuGet 包。输出为 `build\GlassFolders.exe` 和 `build\GlassFolders-Setup.exe`。应用、安装器和测试源码位于 [src](src/)。

运行以下命令进行内置逻辑测试并查看结果。结果文件位于 `build\test-results.txt`。

```powershell
Start-Process -FilePath .\build\GlassFolders.exe -ArgumentList '--self-test' -Wait
Get-Content .\build\test-results.txt
```

## 已知限制

桌面挂靠和模糊效果依赖 Windows 桌面行为。Win+D、隐藏桌面图标、Explorer 重启、多显示器与混合 DPI、登录后自启、完整安装卸载及长列表拖拽滚动仍待完整验证。大目录列表尚未实现虚拟化。
