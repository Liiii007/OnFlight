using System.Text.Json.Serialization;

namespace OnFlight.Core.Settings;

public class AppSettings
{
    [JsonPropertyName("general")]
    public GeneralSettings General { get; set; } = new();

    [JsonPropertyName("appearance")]
    public AppearanceSettings Appearance { get; set; } = new();
}

public class GeneralSettings
{
    [JsonPropertyName("databaseDirectory")]
    public string DatabaseDirectory { get; set; } = string.Empty;
}

public class AppearanceSettings
{
    [JsonPropertyName("themeMode")]
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemeMode
{
    System,
    Light,
    Dark
}
