using System.Text.Json;

namespace UCAD.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    private SettingsService()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UCAD");
        _settingsPath = Path.Combine(root, "settings.json");
        Settings = LoadCore();
    }

    public static SettingsService Current { get; } = new();

    public AppSettings Settings { get; }

    public event EventHandler? SettingsChanged;

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Settings, SerializerOptions));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("SaveSettings", ex);
        }
    }

    public void ResetRecentHistory()
    {
        // Recent-file storage is intentionally not fabricated before file I/O lands.
        // Keep the command as a stable settings-layer entry point for the Start page.
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings LoadCore()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), SerializerOptions)
                   ?? new AppSettings();
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("LoadSettings", ex);
            return new AppSettings();
        }
    }
}
