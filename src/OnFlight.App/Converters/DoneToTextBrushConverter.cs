using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace OnFlight.App.Converters;

/// [Style:Converters] DoneToTextBrush — text color based on IsDone, theme-aware
public class DoneToTextBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush LightDone = new(Color.FromRgb(210, 210, 210));
    private static readonly SolidColorBrush DarkDone  = new(Color.FromRgb(72, 72, 74));    // #48484A
    private static readonly SolidColorBrush LightPending = new(Color.FromRgb(0, 0, 0));
    private static readonly SolidColorBrush DarkPending  = new(Color.FromRgb(255, 255, 255));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        if (value is bool isDone && isDone)
            return isDark ? DarkDone : LightDone;
        return isDark ? DarkPending : LightPending;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
