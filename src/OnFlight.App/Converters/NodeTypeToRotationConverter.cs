using System.Globalization;
using Avalonia.Data.Converters;
using OnFlight.Contracts.Enums;

namespace OnFlight.App.Converters;

/// [Style:Converters] [Style:Icons] NodeTypeToRotation — 180° for Fork/Join icons (vertical flow direction)
public class NodeTypeToRotationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FlowNodeType nodeType)
        {
            return nodeType is FlowNodeType.Fork or FlowNodeType.Join ? 180.0 : 0.0;
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
