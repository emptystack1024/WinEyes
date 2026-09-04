# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working in this repository.

## Project overview

WinEyes is a single-project WPF desktop application that recreates the Unix/Linux `xeyes` utility on Windows. The repository is hosted at https://github.com/emptystack1024/WinEyes.

The project targets `net9.0-windows`, uses the Windows desktop SDK with WPF enabled, and has no external package references, backend, persistence layer, or test project. Development and execution require Windows 10/11 and the .NET 9 SDK; Visual Studio 2022 or later is also supported.

## Common commands

Run these from the repository root in PowerShell:

```powershell
dotnet restore .\WinEyes.sln
dotnet build .\WinEyes.sln -c Debug
dotnet run --project .\WinEyes.csproj -c Debug
dotnet build .\WinEyes.sln -c Release
dotnet publish .\WinEyes.csproj -c Release
```

There is currently no lint configuration or test project. `dotnet test` has no project to run until a test project is added. For a future test project, run all tests with `dotnet test .\path\to\Tests.csproj` and select one test with `dotnet test .\path\to\Tests.csproj --filter "FullyQualifiedName~Namespace.TestClass.TestMethod"`.

Build output is generated under ignored `bin`/`obj` directories; Visual Studio state under `.vs` is also ignored.

## Architecture

- `App.xaml` declares the WPF application and starts `MainWindow.xaml`; `App.xaml.cs` contains no application-level behavior.
- `MainWindow.xaml` defines the borderless, transparent, always-on-top window. A `Canvas` contains two eye grids made from WPF ellipses and owns the context menu for moving, resizing, and exiting.
- `MainWindow.xaml.cs` is the application’s behavior layer. It owns the 16 ms `DispatcherTimer`, startup positioning, cursor tracking, pupil geometry, drag-to-move behavior, and context-menu handlers.
- Cursor coordinates are read from the Windows API through `GetCursorPos` P/Invoke, converted from screen coordinates into window coordinates, and used to move each pupil along a normalized direction vector constrained by the eye size.
- Startup positioning uses `SystemParameters.WorkArea` so the initial window sits near the top-right of the primary monitor without covering the taskbar. Window movement uses WPF mouse capture; the resize menu changes the window dimensions through the three size presets in `MainWindow.xaml.cs`.

Changes to tracking or pupil placement should preserve the distinction between screen coordinates and window/element coordinates. Changes to the visual structure or event names must stay synchronized between `MainWindow.xaml` and its code-behind.

## 维护配置（sync-notion-git 自动收尾）

- 自动分步 git 提交：开启
- 维护 Notion 文档：开启
  - page_id: 3d10b588-b02e-8013-bf8d-cae8e11867fc
- 非常规修改保存 Agent Note：开启

规则：本小节存在且至少一项开启 → 我完成一个完整改动单元、即将结束回合时，自动调用 sync-notion-git skill 执行收尾，不以文字提醒代替；git 开关关闭时不产本地文档。
