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
                return Normalize(new AppSettings());
            }

            return Normalize(
                JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), SerializerOptions)
                ?? new AppSettings());
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("LoadSettings", ex);
            return Normalize(new AppSettings());
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        // Early v0.4.1 acceptance builds persisted a 6 px pickbox before the
        // user-visible size control was reliably reachable. Migrate that one legacy
        // default to the new 10 px baseline, but preserve any later explicit choice.
        if (settings.CadPointerSettingsRevision < 1)
        {
            if (settings.PickboxSize == 6)
            {
                settings.PickboxSize = 10;
            }

            settings.CadPointerSettingsRevision = 1;
        }

        settings.CrosshairSizePercent = Math.Clamp(settings.CrosshairSizePercent, 5, 100);
        settings.PickboxSize = Math.Clamp(settings.PickboxSize, 3, 20);
        settings.ObjectSnapAperture = Math.Clamp(settings.ObjectSnapAperture, 3, 50);
        return settings;
    }
}
