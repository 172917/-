using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MotionStudio.App.ViewModels;

public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is not null && parameter is not null && value.Equals(parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true && parameter is not null ? parameter : Binding.DoNothing;
    }
}
