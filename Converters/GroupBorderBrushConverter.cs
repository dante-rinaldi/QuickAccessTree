using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SidebarBuddy.Converters;

public class GroupBorderBrushConverter : IMultiValueConverter
{
    private static readonly Brush SoftBorder =
        new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not true) return Brushes.Transparent;
        if (values[1] is string hex && !string.IsNullOrEmpty(hex))
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { }
        }
        return SoftBorder;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
