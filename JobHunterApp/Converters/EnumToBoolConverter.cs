using System.Globalization;
using System.Windows.Data;

namespace JobHunterApp.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string p)
            return Enum.Parse(targetType, p);
        return Binding.DoNothing;
    }
}
