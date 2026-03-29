using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace OnFlight.App.Converters;

/// [Style:Converters] DoneToIcon — MaterialIconKind based on IsDone
public class DoneToIconKindConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDone && isDone)
            return MaterialIconKind.CheckCircleOutline;
        return MaterialIconKind.CircleOutline;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
