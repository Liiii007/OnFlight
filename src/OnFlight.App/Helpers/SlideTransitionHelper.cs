using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace OnFlight.App.Helpers;

public static class SlideTransitionHelper
{
    private const double DefaultSlideDistance = 16;
    private const int OutDurationMs = 140;
    private const int InDurationMs = 160;

    private static readonly TransformOperationsTransition SlideOutTx = new()
    {
        Property = Visual.RenderTransformProperty,
        Duration = TimeSpan.FromMilliseconds(OutDurationMs),
        Easing = new CubicEaseIn()
    };

    private static readonly DoubleTransition OpacityOutTx = new()
    {
        Property = Visual.OpacityProperty,
        Duration = TimeSpan.FromMilliseconds(OutDurationMs),
        Easing = new CubicEaseIn()
    };

    private static readonly TransformOperationsTransition SlideInTx = new()
    {
        Property = Visual.RenderTransformProperty,
        Duration = TimeSpan.FromMilliseconds(InDurationMs),
        Easing = new CubicEaseOut()
    };

    private static readonly DoubleTransition OpacityInTx = new()
    {
        Property = Visual.OpacityProperty,
        Duration = TimeSpan.FromMilliseconds(InDurationMs),
        Easing = new CubicEaseOut()
    };

    public static async Task SlideOutAsync(Control target, double distance = -DefaultSlideDistance)
    {
        EnsureTransitions(target, SlideOutTx, OpacityOutTx);

        target.Opacity = 0;
        target.RenderTransform = TransformOperations.Parse($"translateX({distance}px)");

        await Task.Delay(OutDurationMs + 20);
    }

    public static async Task SlideInAsync(Control target, double offset = DefaultSlideDistance)
    {
        RemoveSlideTransitions(target);

        target.Opacity = 0;
        target.RenderTransform = TransformOperations.Parse($"translateX({offset}px)");

        await Task.Delay(16);

        EnsureTransitions(target, SlideInTx, OpacityInTx);

        target.Opacity = 1;
        target.RenderTransform = TransformOperations.Parse("translateX(0px)");

        await Task.Delay(InDurationMs + 20);

        RemoveSlideTransitions(target);
    }

    private static void EnsureTransitions(Control target, params ITransition[] txs)
    {
        target.Transitions ??= new Transitions();
        foreach (var tx in txs)
        {
            if (!target.Transitions.Contains(tx))
                target.Transitions.Add(tx);
        }
    }

    private static void RemoveSlideTransitions(Control target)
    {
        if (target.Transitions == null) return;
        for (int i = target.Transitions.Count - 1; i >= 0; i--)
        {
            var t = target.Transitions[i];
            if (t == SlideOutTx || t == OpacityOutTx || t == SlideInTx || t == OpacityInTx)
                target.Transitions.RemoveAt(i);
        }
    }
}
