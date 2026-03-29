using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;
using OnFlight.Contracts.Enums;

namespace OnFlight.App.Converters;

/// [Style:Converters] [Style:Icons] NodeTypeToIcon — icon per FlowNodeType
public class NodeTypeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FlowNodeType nodeType)
        {
            return nodeType switch
            {
                FlowNodeType.Task => MaterialIconKind.CheckCircle,
                FlowNodeType.Loop => MaterialIconKind.Replay,
                FlowNodeType.Fork => MaterialIconKind.CallSplit,
                FlowNodeType.Join => MaterialIconKind.CallMerge,
                FlowNodeType.Branch => MaterialIconKind.SourceBranch,
                FlowNodeType.ConsoleExecute => MaterialIconKind.Console,
                _ => MaterialIconKind.CheckCircle
            };
        }
        return MaterialIconKind.CheckCircle;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
