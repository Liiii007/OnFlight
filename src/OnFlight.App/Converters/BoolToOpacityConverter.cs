using System.Globalization;
using Avalonia.Data.Converters;

namespace OnFlight.App.Converters;

/// [Style:Converters] BoolToOpacity — locked=0.4 / unlocked=1.0
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLocked = value is bool b && b;
        return isLocked ? 0.4 : 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
