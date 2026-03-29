using System.Globalization;
using Avalonia.Data.Converters;

namespace OnFlight.App.Converters;

/// [Style:Converters] BoolToVis — bool/int/string to IsVisible, supports Invert
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool invert = parameter?.ToString() == "Invert";
        bool boolValue = value switch
        {
            bool b => b,
            int n => n > 0,
            string s => !string.IsNullOrEmpty(s),
            null => false,
            _ => true
        };
        if (invert) boolValue = !boolValue;
        return boolValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;
}
