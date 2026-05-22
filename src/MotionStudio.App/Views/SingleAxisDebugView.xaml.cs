using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MotionStudio.App.ViewModels;

namespace MotionStudio.App.Views;

/// <summary>
/// Single axis debug view.
/// </summary>
public partial class SingleAxisDebugView : UserControl
{
    private Window? _hostWindow;
    private bool _jogPressed;

    public SingleAxisDebugView()
    {
        InitializeComponent();
    }

    private SingleAxisDebugViewModel? ViewModel => DataContext as SingleAxisDebugViewModel;

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, _hostWindow))
        {
            return;
        }

        if (_hostWindow is not null)
        {
            _hostWindow.Deactivated -= HostWindow_Deactivated;
        }

        _hostWindow = window;
        if (_hostWindow is not null)
        {
            _hostWindow.Deactivated += HostWindow_Deactivated;
        }
    }

    private async void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is not null)
        {
            _hostWindow.Deactivated -= HostWindow_Deactivated;
            _hostWindow = null;
        }

        _jogPressed = false;
        await StopJogAndReleaseCaptureAsync(null, force: true);
    }

    private async void HostWindow_Deactivated(object? sender, EventArgs e)
    {
        await StopJogAndReleaseCaptureAsync(null, force: true);
    }

    private async void JogNegativeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        await StartJogWithCaptureAsync(sender as Button, -1);
        e.Handled = true;
    }

    private async void JogPositiveButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        await StartJogWithCaptureAsync(sender as Button, 1);
        e.Handled = true;
    }

    private async void JogButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        await StopJogAndReleaseCaptureAsync(sender as Button, force: true);
        e.Handled = true;
    }

    private async void JogButton_MouseLeave(object sender, MouseEventArgs e)
    {
        await StopJogAndReleaseCaptureAsync(sender as Button);
    }

    private async void JogButton_LostMouseCapture(object sender, MouseEventArgs e)
    {
        await StopJogAndReleaseCaptureAsync(sender as Button);
    }

    private async Task StartJogWithCaptureAsync(Button? button, int direction)
    {
        if (ViewModel is null || button is null)
        {
            return;
        }

        _jogPressed = true;
        var started = direction > 0
            ? await ViewModel.StartJogPositiveAsync()
            : await ViewModel.StartJogNegativeAsync();

        if (started && ViewModel.IsJogRunning)
        {
            button.CaptureMouse();
        }
        else
        {
            _jogPressed = false;
            if (button.IsMouseCaptured)
            {
                button.ReleaseMouseCapture();
            }
        }
    }

    private async Task StopJogAndReleaseCaptureAsync(Button? button, bool force = false)
    {
        if (!force && !_jogPressed)
        {
            return;
        }

        _jogPressed = false;

        if (button is not null && button.IsMouseCaptured)
        {
            button.ReleaseMouseCapture();
        }

        if (JogNegativeButton.IsMouseCaptured)
        {
            JogNegativeButton.ReleaseMouseCapture();
        }

        if (JogPositiveButton.IsMouseCaptured)
        {
            JogPositiveButton.ReleaseMouseCapture();
        }

        if (ViewModel is null || !ViewModel.IsJogRunning)
        {
            return;
        }

        await ViewModel.StopJogAsync();
    }
}
