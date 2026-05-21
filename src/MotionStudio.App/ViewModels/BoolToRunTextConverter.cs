using System.Globalization;
using System.Windows.Data;

namespace MotionStudio.App.ViewModels;

/// <summary>
/// 将运行状态转换为界面状态文本。
/// </summary>
public sealed class BoolToRunTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "运行中" : "空闲";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
