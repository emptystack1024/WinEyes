# Changelog: WinEyes

Status: active

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
