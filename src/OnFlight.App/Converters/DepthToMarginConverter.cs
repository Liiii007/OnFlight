using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OnFlight.App.Converters;

/// [Style:Converters] DepthToMargin — left indent = depth * 24px
public class DepthToMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int depth)
            return new Thickness(depth * 24, 0, 0, 0);
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
