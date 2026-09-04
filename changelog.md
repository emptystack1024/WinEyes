# Changelog: WinEyes

Status: active

## 2026-09-04 — 下调最小尺寸并增加开机自启动

### Problem
现有最小窗口尺寸为 120×60，无法满足更紧凑的桌面摆放需求；应用也缺少从系统托盘管理 Windows 开机自启动的入口。

### Changes
- `MainWindow.xaml`、`MainWindow.xaml.cs`：将最小尺寸统一调整为 60×30，保留 2:1 比例和 900×450 最大尺寸。
- `StartupManager.cs`：使用当前用户 `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run` 管理带引号的绝对可执行文件路径；启用默认关闭，禁用时删除 WinEyes 自有值。
- `MainWindow.xaml.cs`：在托盘菜单增加 `Start with Windows`，按注册表实际状态刷新勾选，并在写入失败时恢复状态和提示用户。
- `README.md`：补充新的尺寸范围和开机自启动使用说明。

### Consequences
- 右键连续缩放、状态恢复和所有尺寸预设现在都不会低于 60×30，窗口仍保持 2:1 构图。
- 自启动仅作用于当前 Windows 用户，不需要管理员权限；移动或删除程序前应先关闭该选项。
- Debug/Release 构建均已通过，均为 0 个警告、0 个错误；通过托盘实机验证了自启动启用与禁用，注册表值可写入并删除。
- 60×30 最小尺寸的完整桌面右键拖动验证仍待手工完成；代码路径已统一使用同一最小宽度和 2:1 比例。

### Alternatives considered
- 未使用 HKLM 或任务计划程序，避免管理员权限和超出单用户桌面工具需求的配置复杂度。
- 未将自启动状态重复保存到 JSON，以注册表实际值作为托盘勾选状态的唯一来源。

## 2026-09-04 — 修复任务栏覆盖置顶

### Problem
Windows 任务栏重新排序后可能覆盖已启用置顶的 WinEyes 窗口，导致窗口位于任务栏区域时暂时不可见；托盘应用也不应额外显示任务栏按钮。

### Changes
- `MainWindow.xaml`：隐藏主窗口的任务栏按钮，保留系统托盘作为控制入口。
- `MainWindow.xaml.cs`：使用 `HWND_TOPMOST` 和 `SWP_NOACTIVATE` 显式恢复窗口 Z 序，在窗口初始化、置顶切换和 `WM_ACTIVATEAPP` 后重新置顶，并增加 250 ms 的低频兜底校正。

### Consequences
- 置顶开启时，任务栏点击后的 Shell 重排序会在最多一个 watchdog 周期内被校正，且不会抢回输入焦点。
- 任务栏、全屏程序、安全桌面或其他系统 UI 仍可能暂时覆盖普通桌面窗口；该行为没有公开的绝对“高于所有 Shell UI”保证。
- 已在 Windows 桌面上启动应用并点击任务栏空白区域验证，窗口在等待 watchdog 周期后仍可见。

### Alternatives considered
- 仅依赖 `WM_ACTIVATEAPP`：任务栏的纯 Z 序变化可能不发送该消息，因此不足以覆盖所有情况。
- `SetWinEventHook` 全局监听：实现复杂度和生命周期管理成本较高，本项目采用低频 watchdog 作为可靠兜底。

## 2026-09-04 — 完善窗口缩放、交互与状态恢复

### Problem
窗口预设尺寸只改变外框，眼睛图形仍使用固定尺寸，导致 Small 裁剪、Large 留白；两个瞳孔的坐标系不一致，跟随鼠标时会出现不同步和偏移。应用也缺少连续缩放、托盘控制、状态恢复和可切换样式。

### Changes
- `MainWindow.xaml`：使用固定设计画布和 `Viewbox` 统一缩放眼睛与瞳孔，增加窗口生命周期及鼠标手势事件。
- `MainWindow.xaml.cs`：修正双眼坐标转换和瞳孔平滑跟随；加入右键连续比例缩放（120×60 至 900×450）、置顶开关、鼠标穿透、系统托盘菜单和内置 Classic/Midnight/Neon 样式。
- `AppSettings.cs`：将窗口位置、宽度、置顶状态和样式保存到 `%LocalAppData%\\WinEyes\\settings.json`，启动时恢复并校正可见位置。
- `WinEyes.csproj`、`App.xaml.cs`：启用 Windows Forms 托盘支持并解决 WPF/Forms 类型冲突。

### Consequences
- Small、Medium、Large 及右键拖动均保持 2:1 构图，窗口不会因眼睛固定尺寸而裁剪。
- 右键现在专用于连续缩放；窗口控制入口迁移到系统托盘，鼠标穿透开启后可从托盘恢复。
- `dotnet build .\\WinEyes.sln -c Debug` 已实机验证通过，结果为 0 个警告、0 个错误。
- 已启动并截图确认窗口可正常渲染和托盘图标可见；自动鼠标注入未能可靠触发透明 WPF 窗口的拖动手势，因此右键连续缩放尚未完成自动化实机验证。
- 尚未增加独立测试项目；多显示器恢复和不同 DPI 下的完整手工验证仍待补充。

### Alternatives considered
- 首版未引入运行时 SVG 解析，改用 WPF 原生矢量元素，避免新增第三方依赖和 SVG 解析维护成本。
- 未保留窗口右键菜单，因为已确认右键始终用于连续缩放，并由系统托盘提供控制入口。
