# Agent Note: 窗口交互与状态恢复
Status: implemented

## Problem
WinEyes 原本把窗口尺寸和视觉尺寸分开处理，Small 会裁剪眼睛，瞳孔位置还混用窗口坐标与嵌套 Canvas 坐标。应用只能通过固定预设调整大小，无法从穿透状态恢复，也不能记住用户上次的窗口状态。

## Decision
使用固定 300×150 的 WPF 设计画布，并通过 `Viewbox` 以统一比例缩放整组眼睛。瞳孔跟随统一使用设计画布和瞳孔宿主的坐标转换，同时对目标偏移做时间平滑和边界限制。右键按住并拖动始终表示连续比例缩放，窗口尺寸限制在 60×30 至 900×450；窗口控制、置顶、鼠标穿透、开机自启动和样式切换统一放入系统托盘菜单。窗口位置、宽度、置顶状态和样式写入 `%LocalAppData%\\WinEyes\\settings.json`，启动时校正到可见屏幕区域。开机自启动状态以当前用户 `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run` 中的带引号可执行文件路径为准，不重复写入 JSON。

## Alternatives considered

### Why not runtime SVG loading?
WPF 没有原生 SVG 控件；首版采用原生 XAML/WPF 矢量样式，避免引入第三方包和运行时解析失败。将来可以在不改变跟随逻辑的前提下增加构建时 SVG 转换。

### Why not keep the window context menu?
用户确认右键始终用于缩放，因此窗口菜单会与连续手势冲突。托盘菜单可以在鼠标穿透后继续提供恢复和退出入口。

### Why not resize the existing fixed eyes independently?
分别修改眼睛和瞳孔的尺寸容易再次产生比例、坐标和裁剪问题；Viewbox 让所有视觉元素共享一个设计坐标系。

## Consequences
- 预设尺寸和连续缩放始终维持 2:1 比例，视觉构图稳定。
- 鼠标穿透状态不跨重启保存，避免下次启动后窗口无法直接操作；托盘仍可切换穿透和置顶。
- 当前设置写入采用临时文件替换，保存失败时清理临时文件。
- 已用 .NET 10 SDK 对 `WinEyes.sln` 完成 Debug 和 Release 构建验证，均为 0 个警告、0 个错误；窗口启动截图验证通过，托盘自启动开关已实机完成启用/禁用往返。自动输入工具仍未能可靠驱动透明窗口的右键拖动。

## Testing
- `dotnet build .\\WinEyes.sln -c Debug`：通过，0 警告、0 错误。
- 启动应用并截图：窗口完整渲染，托盘图标可见。
- 自启动托盘菜单启用/禁用及注册表往返：已完成实机验证。
- 60×30 最小尺寸的右键拖动、多显示器和高 DPI 的完整交互验证：未完成自动化验证。
