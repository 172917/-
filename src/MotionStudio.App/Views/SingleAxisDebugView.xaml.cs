using System.Windows.Controls;
using System.Windows.Input;
using MotionStudio.App.ViewModels;

namespace MotionStudio.App.Views;

/// <summary>
/// 单轴调试视图。
/// </summary>
public partial class SingleAxisDebugView : UserControl
{
    public SingleAxisDebugView()
    {
        InitializeComponent();
    }

    private async void JogNegativeButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            button.CaptureMouse();
        }

        if (DataContext is SingleAxisDebugViewModel viewModel)
        {
            await viewModel.BeginJogAsync(positive: false).ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private async void JogPositiveButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            button.CaptureMouse();
        }

        if (DataContext is SingleAxisDebugViewModel viewModel)
        {
            await viewModel.BeginJogAsync(positive: true).ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private async void JogButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            button.ReleaseMouseCapture();
        }

        if (DataContext is SingleAxisDebugViewModel viewModel)
        {
            await viewModel.EndJogAsync().ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private async void JogButton_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (DataContext is SingleAxisDebugViewModel viewModel)
        {
            await viewModel.EndJogAsync().ConfigureAwait(true);
        }
    }
}
