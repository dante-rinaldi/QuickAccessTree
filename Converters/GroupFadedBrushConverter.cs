using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SidebarBuddy.Converters;

public class GroupFadedBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not true) return Brushes.Transparent;
        if (values[1] is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(Color.FromArgb(40, c.R, c.G, c.B));
            }
            catch { }
        }
        return Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
