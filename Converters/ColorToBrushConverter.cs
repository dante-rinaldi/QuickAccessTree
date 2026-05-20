using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SidebarBuddy.Converters;

public class ColorToBrushConverter : IValueConverter
{
    // Default folder yellow when no custom color is set
    private static readonly SolidColorBrush DefaultBrush =
        new(Color.FromRgb(0xFF, 0xC0, 0x00));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { }
        }
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
