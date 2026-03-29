using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using OnFlight.App.ViewModels;

namespace OnFlight.App.Views;

public partial class FloatingWindow : Window
{
    private static readonly Color LightHeaderTint = Color.Parse("#F0F0F2");
    private static readonly Color DarkHeaderTint = Color.Parse("#1E1E20");

    private FloatingViewModel? _viewModel;

    public FloatingWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyAcrylicForCurrentTheme();
        ActualThemeVariantChanged += (_, _) => ApplyAcrylicForCurrentTheme();
    }

    public FloatingWindow(FloatingViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAvailableListsAsync();
    }

    private void ApplyAcrylicForCurrentTheme()
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var tint = isDark ? DarkHeaderTint : LightHeaderTint;
        if (HeaderAcrylic?.Material is ExperimentalAcrylicMaterial mat)
            mat.TintColor = tint;
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                Owner?.Activate();
                Close();
                return;
            }
            BeginMoveDrag(e);
        }
    }

    private void OnCloseFloating(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
