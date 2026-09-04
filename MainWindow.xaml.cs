using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace WinEyes;

public partial class MainWindow : Window
{
    private const double DesignWidth = 300;
    private const double DesignHeight = 150;
    private const double WindowAspectRatio = DesignWidth / DesignHeight;
    private const double MinimumWindowWidth = 60;
    private const double MaximumWindowWidth = 900;
    private const double PupilTravelFactor = 0.7;
    private const double SmoothingTimeSeconds = 0.07;
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const int WmActivateApp = 0x001C;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly AppSettings settings;
    private readonly DispatcherTimer eyeTrackingTimer;
    private readonly DispatcherTimer topmostEnforcementTimer;
    private readonly IReadOnlyDictionary<string, EyeStyle> eyeStyles = CreateEyeStyles();
    private readonly Dictionary<string, Forms.ToolStripMenuItem> styleMenuItems = new();

    private Forms.NotifyIcon? trayIcon;
    private Forms.ContextMenuStrip? trayMenu;
    private Forms.ToolStripMenuItem? passthroughMenuItem;
    private Forms.ToolStripMenuItem? topmostMenuItem;
    private Forms.ToolStripMenuItem? startupMenuItem;
    private HwndSource? windowSource;
    private IntPtr windowHandle;
    private bool isMousePassthrough;
    private bool isMoving;
    private bool isRightResizing;
    private Point moveStartScreenPoint;
    private double moveStartLeft;
    private double moveStartTop;
    private double moveDpiScaleX = 1;
    private double moveDpiScaleY = 1;
    private Point resizeStartScreenPoint;
    private double resizeStartWidth;
    private double resizeDpiScaleX = 1;
    private double resizeDpiScaleY = 1;
    private Vector leftPupilOffset;
    private Vector rightPupilOffset;
    private DateTime lastTrackingTime;
    private string currentStyleId = "Classic";

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private sealed record EyeStyle(
        string Name,
        Brush EyeFill,
        Brush PupilFill,
        Brush Stroke,
        double StrokeThickness,
        double PupilScale);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetExtendedWindowStyle(IntPtr handle)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, GwlExStyle)
            : GetWindowLong32(handle, GwlExStyle);
    }

    private static void SetExtendedWindowStyle(IntPtr handle, IntPtr style)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(handle, GwlExStyle, style);
        }
        else
        {
            SetWindowLong32(handle, GwlExStyle, style);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    public MainWindow()
    {
        settings = SettingsStore.Load();

        InitializeComponent();

        Topmost = settings.IsTopmost;
        ApplyStyle(settings.StyleId);

        eyeTrackingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        eyeTrackingTimer.Tick += EyeTrackingTimer_Tick;

        topmostEnforcementTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        topmostEnforcementTimer.Tick += (_, _) => EnsureTopmost();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        windowHandle = new WindowInteropHelper(this).Handle;
        windowSource = HwndSource.FromHwnd(windowHandle);
        windowSource?.AddHook(WindowMessageHook);
        EnsureTopmost();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!RestoreWindowState())
        {
            PositionWindowTopRight();
        }

        EnsureTopmost();
        InitializeTrayIcon();
        lastTrackingTime = DateTime.UtcNow;
        eyeTrackingTimer.Start();
        topmostEnforcementTimer.Start();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowState();
        eyeTrackingTimer.Stop();
        topmostEnforcementTimer.Stop();
        isMoving = false;
        isRightResizing = false;
        if (Mouse.Captured == this)
        {
            ReleaseMouseCapture();
        }

        windowSource?.RemoveHook(WindowMessageHook);
        SetMousePassthrough(false);

        if (trayIcon is not null)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }

        trayMenu?.Dispose();
        trayMenu = null;
    }

    private void PositionWindowTopRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + workArea.Width - Width - 20;
        Top = workArea.Top + 20;
    }

    private bool RestoreWindowState()
    {
        if (!settings.HasWindowPosition ||
            !double.IsFinite(settings.Left) ||
            !double.IsFinite(settings.Top) ||
            !double.IsFinite(settings.Width))
        {
            return false;
        }

        Width = Math.Clamp(settings.Width, MinimumWindowWidth, MaximumWindowWidth);
        Height = Width / WindowAspectRatio;
        Left = settings.Left;
        Top = settings.Top;
        KeepWindowVisible();
        return true;
    }

    private void KeepWindowVisible()
    {
        var workArea = SystemParameters.WorkArea;

        if (Width > workArea.Width)
        {
            Width = workArea.Width;
            Height = Width / WindowAspectRatio;
        }

        if (Height > workArea.Height)
        {
            Height = workArea.Height;
            Width = Height * WindowAspectRatio;
        }

        double virtualLeft = SystemParameters.VirtualScreenLeft;
        double virtualTop = SystemParameters.VirtualScreenTop;
        double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        const double minimumVisibleSize = 40;
        bool hasVisiblePart =
            Left + minimumVisibleSize > virtualLeft &&
            Left < virtualRight - minimumVisibleSize &&
            Top + minimumVisibleSize > virtualTop &&
            Top < virtualBottom - minimumVisibleSize;

        if (!hasVisiblePart)
        {
            PositionWindowTopRight();
            return;
        }

        bool overlapsPrimaryWorkArea =
            Left < workArea.Right &&
            Left + Width > workArea.Left &&
            Top < workArea.Bottom &&
            Top + Height > workArea.Top;
        if (overlapsPrimaryWorkArea)
        {
            Left = Math.Clamp(Left, workArea.Left, workArea.Right - Width);
            Top = Math.Clamp(Top, workArea.Top, workArea.Bottom - Height);
        }
    }

    private void EyeTrackingTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsLoaded || designCanvas.ActualWidth <= 0 || designCanvas.ActualHeight <= 0)
        {
            return;
        }

        Point screenPosition = GetMousePosition();
        if (!double.IsFinite(screenPosition.X) || !double.IsFinite(screenPosition.Y))
        {
            return;
        }

        Point mouseInDesignCanvas = designCanvas.PointFromScreen(screenPosition);
        DateTime now = DateTime.UtcNow;
        double elapsedSeconds = Math.Clamp((now - lastTrackingTime).TotalSeconds, 0, 0.2);
        lastTrackingTime = now;
        double smoothing = 1 - Math.Exp(-elapsedSeconds / SmoothingTimeSeconds);

        leftPupilOffset = UpdatePupilPosition(
            leftEye,
            leftPupilCanvas,
            leftEyePupil,
            mouseInDesignCanvas,
            leftPupilOffset,
            smoothing);
        rightPupilOffset = UpdatePupilPosition(
            rightEye,
            rightPupilCanvas,
            rightEyePupil,
            mouseInDesignCanvas,
            rightPupilOffset,
            smoothing);
    }

    private Vector UpdatePupilPosition(
        FrameworkElement eye,
        Canvas pupilCanvas,
        Ellipse pupil,
        Point mouseInDesignCanvas,
        Vector currentOffset,
        double smoothing)
    {
        Point mouseInPupilCanvas = designCanvas
            .TransformToVisual(pupilCanvas)
            .Transform(mouseInDesignCanvas);
        double eyeCenterX = eye.ActualWidth / 2;
        double eyeCenterY = eye.ActualHeight / 2;
        Vector direction = new(
            mouseInPupilCanvas.X - eyeCenterX,
            mouseInPupilCanvas.Y - eyeCenterY);

        if (direction.Length > 0)
        {
            direction.Normalize();
        }

        double maxDistance = Math.Max(
            0,
            (Math.Min(eye.ActualWidth, eye.ActualHeight) - Math.Max(pupil.ActualWidth, pupil.ActualHeight))
                / 2 * PupilTravelFactor);
        Vector targetOffset = direction * maxDistance;
        Vector offset = currentOffset + (targetOffset - currentOffset) * smoothing;

        Canvas.SetLeft(pupil, eyeCenterX + offset.X - pupil.ActualWidth / 2);
        Canvas.SetTop(pupil, eyeCenterY + offset.Y - pupil.ActualHeight / 2);
        return offset;
    }

    private static Point GetMousePosition()
    {
        return GetCursorPos(out POINT point)
            ? new Point(point.X, point.Y)
            : new Point(double.NaN, double.NaN);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (isMousePassthrough || isRightResizing || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        isMoving = true;
        moveStartScreenPoint = GetMousePosition();
        if (!double.IsFinite(moveStartScreenPoint.X) || !double.IsFinite(moveStartScreenPoint.Y))
        {
            isMoving = false;
            return;
        }

        moveStartLeft = Left;
        moveStartTop = Top;
        DpiScale moveDpi = VisualTreeHelper.GetDpi(this);
        moveDpiScaleX = moveDpi.DpiScaleX;
        moveDpiScaleY = moveDpi.DpiScaleY;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!isMoving)
        {
            return;
        }

        isMoving = false;
        if (Mouse.Captured == this)
        {
            ReleaseMouseCapture();
        }

        SaveWindowState();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!isMoving || isRightResizing)
        {
            return;
        }

        Point currentScreenPoint = GetMousePosition();
        if (!double.IsFinite(currentScreenPoint.X) || !double.IsFinite(currentScreenPoint.Y))
        {
            return;
        }

        Vector screenDelta = currentScreenPoint - moveStartScreenPoint;
        Left = moveStartLeft + screenDelta.X / moveDpiScaleX;
        Top = moveStartTop + screenDelta.Y / moveDpiScaleY;
        e.Handled = true;
    }

    private void Window_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (isMousePassthrough || isMoving || e.ChangedButton != MouseButton.Right)
        {
            return;
        }

        isRightResizing = true;
        resizeStartScreenPoint = GetMousePosition();
        if (!double.IsFinite(resizeStartScreenPoint.X) || !double.IsFinite(resizeStartScreenPoint.Y))
        {
            isRightResizing = false;
            return;
        }

        resizeStartWidth = Width;
        DpiScale resizeDpi = VisualTreeHelper.GetDpi(this);
        resizeDpiScaleX = resizeDpi.DpiScaleX;
        resizeDpiScaleY = resizeDpi.DpiScaleY;
        CaptureMouse();
        e.Handled = true;
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!isRightResizing)
        {
            return;
        }

        Point currentScreenPoint = GetMousePosition();
        if (!double.IsFinite(currentScreenPoint.X) || !double.IsFinite(currentScreenPoint.Y))
        {
            return;
        }

        Vector screenDelta = currentScreenPoint - resizeStartScreenPoint;
        double diagonalDelta = (screenDelta.X / resizeDpiScaleX + screenDelta.Y / resizeDpiScaleY)
            / Math.Sqrt(2);
        double width = Math.Clamp(
            resizeStartWidth + diagonalDelta,
            MinimumWindowWidth,
            MaximumWindowWidth);

        SetWindowWidth(width);
        e.Handled = true;
    }

    private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isRightResizing || e.ChangedButton != MouseButton.Right)
        {
            return;
        }

        isRightResizing = false;
        if (Mouse.Captured == this)
        {
            ReleaseMouseCapture();
        }

        SaveWindowState();
        e.Handled = true;
    }

    private void Window_LostMouseCapture(object sender, MouseEventArgs e)
    {
        bool interactionEnded = isMoving || isRightResizing;
        isMoving = false;
        isRightResizing = false;
        if (interactionEnded)
        {
            SaveWindowState();
        }
    }

    private void ResizeWindow(double width)
    {
        SetWindowWidth(width);
        SaveWindowState();
    }

    private void SetWindowWidth(double width)
    {
        double clampedWidth = Math.Clamp(width, MinimumWindowWidth, MaximumWindowWidth);
        if (Math.Abs(Width - clampedWidth) < 0.01)
        {
            return;
        }

        Width = clampedWidth;
        Height = clampedWidth / WindowAspectRatio;
        KeepWindowVisible();
    }

    private void InitializeTrayIcon()
    {
        if (trayIcon is not null)
        {
            return;
        }

        trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Opening += (_, _) => UpdateTrayMenuState();

        var showWindowItem = new Forms.ToolStripMenuItem("Show WinEyes");
        showWindowItem.Click += (_, _) => ShowWindow();
        trayMenu.Items.Add(showWindowItem);

        trayMenu.Items.Add(new Forms.ToolStripSeparator());

        topmostMenuItem = new Forms.ToolStripMenuItem("Always on top")
        {
            CheckOnClick = true,
            Checked = Topmost
        };
        topmostMenuItem.Click += (_, _) =>
        {
            Topmost = topmostMenuItem.Checked;
            EnsureTopmost();
            SaveWindowState();
        };
        trayMenu.Items.Add(topmostMenuItem);

        startupMenuItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = false
        };
        startupMenuItem.Click += StartupMenuItem_Click;
        trayMenu.Items.Add(startupMenuItem);

        passthroughMenuItem = new Forms.ToolStripMenuItem("Mouse passthrough")
        {
            CheckOnClick = true
        };
        passthroughMenuItem.Click += (_, _) => SetMousePassthrough(passthroughMenuItem.Checked);
        trayMenu.Items.Add(passthroughMenuItem);

        var resizeMenu = new Forms.ToolStripMenuItem("Resize");
        AddResizeMenuItem(resizeMenu, "Small", 200);
        AddResizeMenuItem(resizeMenu, "Medium", 300);
        AddResizeMenuItem(resizeMenu, "Large", 400);
        trayMenu.Items.Add(resizeMenu);

        var styleMenu = new Forms.ToolStripMenuItem("Style");
        foreach (var style in eyeStyles)
        {
            var styleItem = new Forms.ToolStripMenuItem(style.Value.Name)
            {
                CheckOnClick = true,
                Tag = style.Key,
                Checked = style.Key == currentStyleId
            };
            styleItem.Click += StyleMenuItem_Click;
            styleMenuItems[style.Key] = styleItem;
            styleMenu.DropDownItems.Add(styleItem);
        }

        trayMenu.Items.Add(styleMenu);
        trayMenu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Close();
        trayMenu.Items.Add(exitItem);

        trayIcon = new Forms.NotifyIcon
        {
            Text = "WinEyes",
            Icon = Drawing.SystemIcons.Application,
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => ShowWindow();
        UpdateTrayMenuState();
    }

    private void StartupMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not Forms.ToolStripMenuItem item)
        {
            return;
        }

        bool enabled = !item.Checked;
        if (!StartupManager.TrySetEnabled(enabled))
        {
            System.Windows.MessageBox.Show(
                this,
                "Unable to update the Windows startup setting.",
                "WinEyes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        UpdateTrayMenuState();
    }

    private void AddResizeMenuItem(Forms.ToolStripMenuItem parent, string name, double width)
    {
        var item = new Forms.ToolStripMenuItem(name);
        item.Click += (_, _) => ResizeWindow(width);
        parent.DropDownItems.Add(item);
    }

    private void StyleMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not Forms.ToolStripMenuItem item || item.Tag is not string styleId)
        {
            return;
        }

        ApplyStyle(styleId);
        settings.StyleId = currentStyleId;
        SaveWindowState();
        UpdateTrayMenuState();
    }

    private void ApplyStyle(string? styleId)
    {
        if (styleId is null || !eyeStyles.ContainsKey(styleId))
        {
            styleId = "Classic";
        }

        currentStyleId = styleId;
        EyeStyle style = eyeStyles[currentStyleId];
        leftEyeBackground.Fill = style.EyeFill;
        rightEyeBackground.Fill = style.EyeFill;
        leftEyeBackground.Stroke = style.Stroke;
        rightEyeBackground.Stroke = style.Stroke;
        leftEyeBackground.StrokeThickness = style.StrokeThickness;
        rightEyeBackground.StrokeThickness = style.StrokeThickness;
        leftEyePupil.Fill = style.PupilFill;
        rightEyePupil.Fill = style.PupilFill;

        double pupilSize = 40 * style.PupilScale;
        leftEyePupil.Width = pupilSize;
        leftEyePupil.Height = pupilSize;
        rightEyePupil.Width = pupilSize;
        rightEyePupil.Height = pupilSize;
        leftPupilOffset = new Vector();
        rightPupilOffset = new Vector();
        Canvas.SetLeft(leftEyePupil, 60 - pupilSize / 2);
        Canvas.SetTop(leftEyePupil, 60 - pupilSize / 2);
        Canvas.SetLeft(rightEyePupil, 60 - pupilSize / 2);
        Canvas.SetTop(rightEyePupil, 60 - pupilSize / 2);
    }

    private void EnsureTopmost()
    {
        if (!Topmost || windowHandle == IntPtr.Zero ||
            Visibility != Visibility.Visible || WindowState == WindowState.Minimized)
        {
            return;
        }

        SetWindowPos(
            windowHandle,
            HwndTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void SetMousePassthrough(bool enabled)
    {
        isMousePassthrough = enabled;
        if (windowHandle != IntPtr.Zero)
        {
            long extendedStyle = GetExtendedWindowStyle(windowHandle).ToInt64();
            extendedStyle = enabled
                ? extendedStyle | WsExTransparent
                : extendedStyle & ~WsExTransparent;
            SetExtendedWindowStyle(windowHandle, new IntPtr(extendedStyle));
            SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
        }

        if (passthroughMenuItem is not null)
        {
            passthroughMenuItem.Checked = enabled;
        }
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmActivateApp && Topmost && wParam == IntPtr.Zero)
        {
            EnsureTopmost();
            if (!Dispatcher.HasShutdownStarted)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(EnsureTopmost));
            }
        }

        if (message == WmNcHitTest && isMousePassthrough)
        {
            handled = true;
            return new IntPtr(HtTransparent);
        }

        return IntPtr.Zero;
    }

    private void ShowWindow()
    {
        if (Visibility != Visibility.Visible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void UpdateTrayMenuState()
    {
        if (topmostMenuItem is not null)
        {
            topmostMenuItem.Checked = Topmost;
        }

        if (passthroughMenuItem is not null)
        {
            passthroughMenuItem.Checked = isMousePassthrough;
        }

        if (startupMenuItem is not null)
        {
            startupMenuItem.Checked = StartupManager.IsEnabled();
        }

        foreach (var styleItem in styleMenuItems)
        {
            styleItem.Value.Checked = styleItem.Key == currentStyleId;
        }
    }

    private void SaveWindowState()
    {
        if (!IsLoaded)
        {
            return;
        }

        settings.HasWindowPosition = true;
        settings.Left = Left;
        settings.Top = Top;
        settings.Width = Width;
        settings.IsTopmost = Topmost;
        settings.StyleId = currentStyleId;
        SettingsStore.Save(settings);
    }

    private static IReadOnlyDictionary<string, EyeStyle> CreateEyeStyles()
    {
        return new Dictionary<string, EyeStyle>
        {
            ["Classic"] = new(
                "Classic",
                CreateBrush("#FFFFFF"),
                CreateBrush("#000000"),
                CreateBrush("#000000"),
                2,
                1),
            ["Midnight"] = new(
                "Midnight",
                CreateBrush("#DCE7F2"),
                CreateBrush("#111827"),
                CreateBrush("#5EEAD4"),
                2.5,
                0.95),
            ["Neon"] = new(
                "Neon",
                CreateBrush("#10131A"),
                CreateBrush("#00F5D4"),
                CreateBrush("#FF4ECD"),
                2,
                0.9)
        };
    }

    private static Brush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)!);
        brush.Freeze();
        return brush;
    }
}
