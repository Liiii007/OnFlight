using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OnFlight.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task SaveAsync();
    string GetDatabasePath();
}

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;

    public AppSettings Current { get; private set; }

    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger ?? NullLogger<SettingsService>.Instance;
        var exeDir = AppContext.BaseDirectory;
        _settingsFilePath = Path.Combine(exeDir, "settings.json");
        Current = Load();
    }

    private AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            var defaults = CreateDefaults();
            SaveSync(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? CreateDefaults();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings from {Path}, falling back to defaults", _settingsFilePath);
            return CreateDefaults();
        }
    }

    private static AppSettings CreateDefaults()
    {
        return new AppSettings
        {
            General = new GeneralSettings
            {
                DatabaseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OnFlight")
            },
            Appearance = new AppearanceSettings
            {
                ThemeMode = ThemeMode.System,
            }
        };
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(Current, JsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
        _logger.LogInformation("Settings saved to {Path}", _settingsFilePath);
    }

    private void SaveSync(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    public string GetDatabasePath()
    {
        var dbDir = string.IsNullOrWhiteSpace(Current.General.DatabaseDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OnFlight")
            : Current.General.DatabaseDirectory;

        Directory.CreateDirectory(dbDir);
        return Path.Combine(dbDir, "onflight.db");
    }
}
