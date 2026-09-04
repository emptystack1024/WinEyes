using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices; // For Win32 APIs

namespace WinEyes;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Eye tracking properties
    private Point mousePosition;
    private DispatcherTimer eyeTrackingTimer;
    
    // Window moving
    private bool isMoving = false;
    private Point startPoint;
    
    // Sizes
    private readonly double[] sizesWidth = { 200, 300, 400 };
    private readonly double[] sizesHeight = { 100, 150, 200 };
    private int currentSizeIndex = 1; // Start with medium size

    // P/Invoke structure for mouse position
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize and start the eye tracking timer
        eyeTrackingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        eyeTrackingTimer.Tick += EyeTrackingTimer_Tick;
        
        // Position window at top right corner of the screen on startup
        Loaded += (s, e) => 
        {
            PositionWindowTopRight();
            eyeTrackingTimer.Start(); // Start tracking after window is loaded
        };
    }
    
    private void PositionWindowTopRight()
    {
        // Get the primary screen working area (screen minus taskbar)
        double screenWidth = SystemParameters.WorkArea.Width;
        double screenHeight = SystemParameters.WorkArea.Height;
        double screenLeft = SystemParameters.WorkArea.Left;
        double screenTop = SystemParameters.WorkArea.Top;
        
        // Set window position to top right
        Left = screenLeft + screenWidth - Width - 20;
        Top = screenTop + 20;
    }

    private void EyeTrackingTimer_Tick(object sender, EventArgs e)
    {
        // Get the current mouse position relative to the screen
        mousePosition = GetMousePosition();
        
        // Update the eye positions
        UpdateEyePositions();
    }
    
    private void UpdateEyePositions()
    {
        Point mouseInWindow = PointFromScreen(mousePosition);
        
        UpdatePupilPosition(leftEyeBackground, leftEyePupil, mouseInWindow);
        UpdatePupilPosition(rightEyeBackground, rightEyePupil, mouseInWindow);
    }
    
    private void UpdatePupilPosition(Ellipse eye, Ellipse pupil, Point mouseInWindow)
    {
        if (eye == null || pupil == null) return;
        
        // Get the center of the eye in window coordinates
        Point eyePosition = eye.TransformToAncestor(this).Transform(new Point(0, 0));
        double eyeCenterX = eyePosition.X + eye.ActualWidth / 2;
        double eyeCenterY = eyePosition.Y + eye.ActualHeight / 2;
        
        // Calculate direction vector from eye center to mouse
        double directionX = mouseInWindow.X - eyeCenterX;
        double directionY = mouseInWindow.Y - eyeCenterY;
        
        // Normalize the direction
        double length = Math.Sqrt(directionX * directionX + directionY * directionY);
        if (length > 0)
        {
            directionX /= length;
            directionY /= length;
        }
        
        // Limit pupil movement to stay within the eye (with some margin)
        double maxDistance = (eye.ActualWidth - pupil.ActualWidth) / 2 * 0.7;
        
        // Calculate new pupil position
        double pupilX = eyeCenterX + directionX * maxDistance - eyePosition.X - pupil.ActualWidth / 2;
        double pupilY = eyeCenterY + directionY * maxDistance - eyePosition.Y - pupil.ActualHeight / 2;
        
        // Set the new position
        Canvas canvas = pupil.Parent as Canvas;
        if (canvas != null)
        {
            Canvas.SetLeft(pupil, pupilX);
            Canvas.SetTop(pupil, pupilY);
        }
    }
    
    private static Point GetMousePosition()
    {
        GetCursorPos(out POINT point);
        return new Point(point.X, point.Y);
    }
    
    // Window movement handling
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        isMoving = true;
        startPoint = e.GetPosition(this);
        CaptureMouse();
    }
    
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        isMoving = false;
        ReleaseMouseCapture();
    }
    
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isMoving)
        {
            Point currentPosition = e.GetPosition(this);
            Vector offset = currentPosition - startPoint;
            
            Left += offset.X;
            Top += offset.Y;
        }
    }
    
    // Context menu handlers
    private void MoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Click and drag the eyes to move them.", "Move Eyes");
    }
    
    private void ResizeSmallMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResizeWindow(0);
    }
    
    private void ResizeMediumMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResizeWindow(1);
    }
    
    private void ResizeLargeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResizeWindow(2);
    }
    
    private void ResizeWindow(int sizeIndex)
    {
        currentSizeIndex = sizeIndex;
        Width = sizesWidth[sizeIndex];
        Height = sizesHeight[sizeIndex];
    }
    
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}