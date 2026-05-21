using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HandyControl.Controls;
using MotionStudio.App.ViewModels;
using MotionStudio.App.Views;

namespace MotionStudio.App;

/// <summary>
/// MotionStudio 主窗口，承载 HandyControl 工业软件布局。11
/// </summary>
public partial class MainWindow : HandyControl.Controls.Window
{
    private const int SnapThreshold = 24;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private MotionLogWindow? _logWindow;

    public MainWindow()
    {
        InitializeComponent();
        Growl.GrowlPanel = GrowlHost;
        DataContext = new MainViewModel();
    }

    private void OpenLogWindow_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_logWindow is { IsVisible: true })
        {
            if (_logWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _logWindow.WindowState = System.Windows.WindowState.Normal;
            }
           
            _logWindow.Activate();
            return;
        }

        _logWindow = new MotionLogWindow
        {
            Owner = this,
            DataContext = DataContext
        };
        _logWindow.Closed += (_, _) => _logWindow = null;
        _logWindow.Show();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractiveElement(e.OriginalSource as DependencyObject, TitleBar))
        {
            return;
        }

        try
        {
            if (WindowState == WindowState.Maximized)
            {
                RestoreWindowForDrag(e);
            }

            DragMove();
            SnapToCurrentWorkArea();
        }
        catch (InvalidOperationException)
        {
        }

        e.Handled = true;
    }

    private void RestoreWindowForDrag(MouseButtonEventArgs e)
    {
        var mouseOnWindow = e.GetPosition(this);
        var restoreWidth = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width;
        var horizontalRatio = ActualWidth <= 0 ? 0.5 : mouseOnWindow.X / ActualWidth;
        var mouseOnScreen = PointToScreen(mouseOnWindow);
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var mouseDip = transform.Transform(mouseOnScreen);

        WindowState = WindowState.Normal;
        Left = mouseDip.X - restoreWidth * horizontalRatio;
        Top = mouseDip.Y - 18;
    }

    private void SnapToCurrentWorkArea()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var workArea = GetCurrentMonitorWorkArea();
        if (Math.Abs(Top - workArea.Top) <= SnapThreshold)
        {
            WindowState = WindowState.Maximized;
            return;
        }

        if (Math.Abs(Left - workArea.Left) <= SnapThreshold)
        {
            Left = workArea.Left;
        }
        else if (Math.Abs((Left + ActualWidth) - workArea.Right) <= SnapThreshold)
        {
            Left = workArea.Right - ActualWidth;
        }

        if (Math.Abs((Top + ActualHeight) - workArea.Bottom) <= SnapThreshold)
        {
            Top = workArea.Bottom - ActualHeight;
        }
    }

    private Rect GetCurrentMonitorWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return SystemParameters.WorkArea;
        }

        return ConvertDeviceRectToDip(info.WorkArea);
    }

    private Rect ConvertDeviceRectToDip(NativeRect rect)
    {
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(rect.Left, rect.Top));
        var bottomRight = transform.Transform(new Point(rect.Right, rect.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private static bool IsInteractiveElement(DependencyObject? source, DependencyObject stopAt)
    {
        while (source is not null && source != stopAt)
        {
            if (source is ButtonBase
                or TextBoxBase
                or Selector
                or MenuItem
                or Slider
                or ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}
