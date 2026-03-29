using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OnFlight.Contracts.Enums;

namespace OnFlight.App.Converters;

/// [Style:Converters] StatusToStrikethrough — strikethrough when Done
public class StatusToStrikethroughConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TodoStatus status && status == TodoStatus.Done)
            return TextDecorationCollection.Parse("Strikethrough");
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
