using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using OnFlight.Core.Data;
using OnFlight.Core.Settings;
using System.Collections.ObjectModel;

namespace OnFlight.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly DatabaseInitializer _dbInitializer;

    private bool _isLoading;

    [ObservableProperty] private SettingsCategoryItem? _selectedCategory;
    [ObservableProperty] private string _databaseDirectory = string.Empty;
    [ObservableProperty] private ThemeMode _selectedThemeMode;

    public bool IsGeneralVisible => SelectedCategory?.Key == "general";
    public bool IsAppearanceVisible => SelectedCategory?.Key == "appearance";

    public ObservableCollection<SettingsCategoryItem> Categories { get; } = new();
    public ThemeMode[] ThemeModeValues { get; } = Enum.GetValues<ThemeMode>();

    public event Action? ThemeChanged;
    public event Action? SettingsSaved;
    public event Func<Task<bool>>? ClearDataRequested;
    public event Action? DataCleared;

    public SettingsViewModel(ISettingsService settingsService, DatabaseInitializer dbInitializer)
    {
        _settingsService = settingsService;
        _dbInitializer = dbInitializer;

        Categories.Add(new SettingsCategoryItem("General", MaterialIconKind.CogOutline, "general"));
        Categories.Add(new SettingsCategoryItem("Appearance", MaterialIconKind.PaletteOutline, "appearance"));

        LoadFromSettings();
        SelectedCategory = Categories[0];
    }

    private void LoadFromSettings()
    {
        _isLoading = true;
        var s = _settingsService.Current;
        DatabaseDirectory = s.General.DatabaseDirectory;
        SelectedThemeMode = s.Appearance.ThemeMode;
        _isLoading = false;
    }

    partial void OnSelectedCategoryChanged(SettingsCategoryItem? value)
    {
        OnPropertyChanged(nameof(IsGeneralVisible));
        OnPropertyChanged(nameof(IsAppearanceVisible));
    }

    partial void OnDatabaseDirectoryChanged(string value)
    {
        ApplySettingsAsync().ConfigureAwait(false);
    }

    partial void OnSelectedThemeModeChanged(ThemeMode value)
    {
        ApplySettingsAsync().ConfigureAwait(false);
    }

    private async Task ApplySettingsAsync()
    {
        if (_isLoading) return;

        var s = _settingsService.Current;
        var themeChanged = s.Appearance.ThemeMode != SelectedThemeMode;

        s.General.DatabaseDirectory = DatabaseDirectory;
        s.Appearance.ThemeMode = SelectedThemeMode;

        await _settingsService.SaveAsync();

        if (themeChanged)
            ThemeChanged?.Invoke();

        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        if (ClearDataRequested != null)
        {
            var confirmed = await ClearDataRequested.Invoke();
            if (!confirmed) return;
        }

        _dbInitializer.ClearAllData();
        DataCleared?.Invoke();
    }
}

public class SettingsCategoryItem
{
    public string Name { get; }
    public MaterialIconKind IconKind { get; }
    public string Key { get; }

    public SettingsCategoryItem(string name, MaterialIconKind iconKind, string key)
    {
        Name = name;
        IconKind = iconKind;
        Key = key;
    }

    public override string ToString() => Name;
}
