using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OnFlight.App.Converters;

/// [Style:Converters] HexToBrush — parse hex string to SolidColorBrush
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                var color = Color.Parse(hex);
                return new SolidColorBrush(color);
            }
            catch { }
        }
        return new SolidColorBrush(Colors.Orange);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
