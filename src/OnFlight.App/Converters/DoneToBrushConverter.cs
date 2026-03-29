using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace OnFlight.App.Converters;

/// [Style:Converters] DoneToBrush — checkbox icon color based on IsDone, theme-aware
public class DoneToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush LightDone   = new(Color.FromRgb(210, 210, 210));
    private static readonly SolidColorBrush DarkDone    = new(Color.FromRgb(72, 72, 74));    // #48484A
    private static readonly SolidColorBrush LightUndone = new(Color.FromRgb(200, 200, 200));
    private static readonly SolidColorBrush DarkUndone  = new(Color.FromRgb(99, 99, 102));   // #636366

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        if (value is bool isDone && isDone)
            return isDark ? DarkDone : LightDone;
        return isDark ? DarkUndone : LightUndone;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
