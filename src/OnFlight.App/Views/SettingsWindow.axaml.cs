using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using OnFlight.App.ViewModels;

namespace OnFlight.App.Views;

public partial class SettingsWindow : Window
{
    private static readonly Color LightSidebarTint = Color.Parse("#F7F7FA");
    private static readonly Color DarkSidebarTint = Color.Parse("#1E1E20");

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.ClearDataRequested += OnClearDataRequested;

        Loaded += (_, _) => ApplyAcrylicForCurrentTheme();
        ActualThemeVariantChanged += (_, _) => ApplyAcrylicForCurrentTheme();
    }

    private void ApplyAcrylicForCurrentTheme()
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var tint = isDark ? DarkSidebarTint : LightSidebarTint;

        if (SidebarAcrylic.Material is ExperimentalAcrylicMaterial m)
            m.TintColor = tint;
    }

    private async Task<bool> OnClearDataRequested()
    {
        var result = await ConfirmDialog.ShowAsync(
            this,
            "Clear All Data",
            "This will permanently delete all task lists, items, running instances and operation logs. This action cannot be undone.",
            new DialogButton { Text = "Cancel", ResultId = "cancel" },
            new DialogButton { Text = "Clear All", ResultId = "confirm", IsDestructive = true });

        return result == "confirm";
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnBrowseDbDirectory(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Database Directory",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path) && DataContext is SettingsViewModel vm)
            {
                vm.DatabaseDirectory = path;
            }
        }
    }
}
