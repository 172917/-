using System.Globalization;
using System.Windows.Data;

namespace MotionStudio.App.ViewModels;

/// <summary>
/// 将 ListBox 的 AlternationIndex 转换为从 1 开始的流程显示序号。
/// </summary>
public sealed class FlowIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int index ? $"# {index + 1}" : "# -";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
