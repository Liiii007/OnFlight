using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OnFlight.Contracts.Enums;

namespace OnFlight.App.Converters;

/// [Style:Converters] StatusToBrush — accent color per TodoStatus
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TodoStatus status)
        {
            return status switch
            {
                TodoStatus.Done => new SolidColorBrush(Color.FromRgb(52, 199, 89)),       // #34C759
                TodoStatus.Ready => new SolidColorBrush(Color.FromRgb(0, 122, 255)), // #007AFF
                TodoStatus.Skipped => new SolidColorBrush(Color.FromRgb(142, 142, 147)),  // #8E8E93
                _ => new SolidColorBrush(Color.FromRgb(255, 149, 0))                      // #FF9500
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
