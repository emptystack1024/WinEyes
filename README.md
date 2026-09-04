# WinEyes - Xeyes for Windows

![WinEyes](WinEyes_Screenshot.png)

WinEyes is a Windows implementation of the classic Unix/Linux xeyes utility, built with C# and WPF. 

## What is WinEyes?

WinEyes displays a pair of eyes on your screen that follow your mouse cursor, just like the classic xeyes application found in X Window System environments. The eyes track your mouse cursor in real-time, creating a fun and nostalgic desktop accessory.

## Features

- **Transparent Window**: Eyes appear to float on your desktop
- **Always on Top**: Toggle the always-on-top behavior from the system tray
- **Mouse Tracking**: Both eyes smoothly follow your mouse cursor
- **Window Movement**: Drag the eyes with the left mouse button when passthrough is disabled
- **Continuous Resize**: Hold the right mouse button and drag down-right to enlarge or up-left to shrink
- **System Tray Controls**: Toggle mouse passthrough, always-on-top, preset sizes, built-in styles, show/restore, and exit
- **State Restoration**: The last position, size, topmost state, and selected style are restored on the next launch
- **Built-in Styles**: Classic, Midnight, and Neon XAML vector styles

## Motivation

This project was created to demonstrate:
1. How classic Unix/Linux utilities can be reimplemented in modern Windows environments
2. The power of GitHub Copilot's agent mode for quickly building functional applications
3. The ease of working with transparent, borderless windows in WPF

## How This Project Was Created

This project was generated entirely using GitHub Copilot, demonstrating the AI's ability to create a complete working application from a simple description. 

The project was initiated with the following prompt:

```
please write a xeyes program! the requirements are:
- the program must look like and behave like the famous xeyes known from unix systems.
- the program must run in Windows 11. Prefered technology is C#/WPF. Therefore the name of the program is "WinEyes".
- the program window should have no borders, the background should be transparent.
- on startup the eyes must be located in the right upper corner of the main screen.
- right click on one of the eyes must open a menu that offers moving and sizing the eyes or terminating the process.
- the project will be published on github and must contain a readme file in markdown format.
this project should demonstrate how easy programming can be and how powerful the github Copilot Agent mode is.
the readme file should describe what the motivation was and how this project was created, including this prompt.
please also add a summarization of the plan and the steps you executed to write this program.
```

## Implementation Steps

1. **Project Setup**: Created a new C#/WPF project targeting .NET 9
2. **Window Configuration**: Configured a borderless, transparent window
3. **Eye Design**: Implemented the eye graphics using WPF ellipses
4. **Mouse Tracking**: Added real-time tracking of the mouse cursor
5. **Pupil Movement**: Calculated smooth pupil positioning in a shared design coordinate system
6. **Window Positioning**: Set initial position to the top-right corner and restore the last valid position
7. **Continuous Resizing**: Added right-button drag resizing with fixed aspect ratio and size limits
8. **System Tray Controls**: Added tray controls for passthrough, topmost, sizes, styles, restoring, and exiting
9. **State Persistence**: Added local JSON persistence for window state and selected style
10. **Documentation**: Created this README.md with screenshots and instructions

## Running the Application

To run WinEyes:

1. Clone the repository
2. Open the solution in Visual Studio 2022 or later
3. Build and run the project
4. Use the system tray icon to access settings and exit
5. Use the left mouse button to move the window and the right mouse button to resize it

## Requirements

- Windows 10/11
- .NET 9.0 SDK or later
- Visual Studio 2022 or later (for development)

## License

This project is open source and available under the MIT License.

## Acknowledgements

- Inspired by the classic xeyes utility from the X Window System
- Created with the assistance of GitHub Copilot