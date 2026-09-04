# Agent Note: 任务栏区域置顶校正
Status: implemented

## Problem
WPF 的 `Topmost` 只把窗口放入顶层窗口组；Windows 任务栏同样处于顶层，Shell 在任务栏被点击或恢复时可能重新调整 Z 序，使位于任务栏区域的 WinEyes 被覆盖。任务栏应用还不需要额外显示主窗口按钮。

## Decision
显式使用 `SetWindowPos(HWND_TOPMOST, ..., SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)` 恢复 Z 序。窗口初始化、置顶切换和 `WM_ACTIVATEAPP` 失焦路径执行即时校正，并以 250 ms 的 `DispatcherTimer` 作为任务栏纯 Z 序变化未产生窗口消息时的兜底。主窗口设置 `ShowInTaskbar=False`，系统托盘作为唯一控制入口。

## Alternatives considered

### Why not rely on `Window.Topmost` alone?
它不能保证高于同属顶层组的任务栏，Shell 重新排序后可能覆盖窗口。

### Why not use only `WM_ACTIVATEAPP`?
任务栏改变 Z 序时不一定改变前台应用，因此可能不发送该消息。

### Why not use `SetWinEventHook`?
全局事件钩子需要额外的委托保活、消息线程和卸载生命周期管理；对当前单窗口程序，低频 watchdog 更简单且覆盖更广。

## Consequences
- 置顶开启时，任务栏点击后的覆盖最多持续一个 watchdog 周期，校正过程不抢夺输入焦点。
- 定期调整 Z 序可能压过其他临时顶层窗口；全屏程序、安全桌面和部分系统 UI 仍可覆盖 WinEyes。
- 已在 Windows 桌面启动应用并点击任务栏空白区域验证，等待一个 watchdog 周期后窗口仍可见。
