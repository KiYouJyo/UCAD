using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;

namespace UCAD.Services;

/// <summary>
/// Owns UCAD's runtime language context. ResourceLoader instances capture a resource
/// context, so they must be recreated after PrimaryLanguageOverride changes.
/// </summary>
public sealed class LocalizationService
{
    private const string DefaultMapName = "Resources";
    private const string V039MapName = "UcadV039";
    private const string LiveReloadNoteKey = "Settings_Language_ReloadNote";
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN",
        "ja-JP",
        "en-US"
    };

    private readonly Dictionary<string, ResourceLoader> _loaders = new(StringComparer.OrdinalIgnoreCase);

    private LocalizationService()
    {
    }

    public static LocalizationService Current { get; } = new();

    public string AppliedLanguageOverride { get; private set; } = string.Empty;

    public int Generation { get; private set; }

    public bool IsSettingsLanguageApplied
    {
        get
        {
            var settings = SettingsService.Current.Settings;
            return string.Equals(
                AppliedLanguageOverride,
                ResolveOverride(settings.DisplayLanguage, settings.FollowSystemLanguage),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public string CurrentLanguageTag
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AppliedLanguageOverride))
            {
                return AppliedLanguageOverride;
            }

            try
            {
                return ApplicationLanguages.Languages.FirstOrDefault() ?? "zh-CN";
            }
            catch
            {
                return "zh-CN";
            }
        }
    }

    public bool ApplyFromSettings(bool writeLog = true)
    {
        var settings = SettingsService.Current.Settings;
        return ApplyLanguagePreference(settings.DisplayLanguage, settings.FollowSystemLanguage, writeLog);
    }

    public bool ApplyLanguagePreference(string? displayLanguage, bool followSystemLanguage, bool writeLog = true)
    {
        var language = ResolveOverride(displayLanguage, followSystemLanguage);

        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = language;
        }
        catch (InvalidOperationException)
        {
            if (writeLog)
            {
                App.WriteStartupEvent("Display language override unavailable in unpackaged runtime; keeping the current Windows resource context");
            }
            return false;
        }

        AppliedLanguageOverride = language;
        _loaders.Clear();
        Generation++;

        if (writeLog)
        {
            App.WriteStartupEvent(string.IsNullOrEmpty(language)
                ? "Display language applied live: system preference"
                : $"Display language applied live: {language}");
        }

        return true;
    }

    public string GetString(string key)
    {
        var value = GetStringFromMap(key, DefaultMapName);
        return string.IsNullOrWhiteSpace(value) ? GetV039String(key) : value;
    }

    public string GetV039String(string key)
    {
        if (string.Equals(key, LiveReloadNoteKey, StringComparison.Ordinal))
        {
            return CurrentLanguageTag switch
            {
                "ja-JP" => "表示言語は現在のウィンドウ、Start、Settings、既存の図面タブへすぐに反映されます。UCAD の再起動は不要です。",
                "en-US" => "Display-language changes apply immediately to the current window, Start, Settings, and existing drawing tabs. No UCAD restart is required.",
                _ => "显示语言会立即应用到当前窗口、Start、Settings 与现有图纸标签，无需重启 UCAD。"
            };
        }

        return GetStringFromMap(key, V039MapName);
    }

    public string GetStringFromMap(string key, string mapName)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        try
        {
            if (!_loaders.TryGetValue(mapName, out var loader))
            {
                loader = CreateLoader(mapName);
                _loaders[mapName] = loader;
            }

            return loader.GetString(key) ?? string.Empty;
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure($"Localization:{mapName}:{key}", ex);
            return string.Empty;
        }
    }

    private static ResourceLoader CreateLoader(string mapName)
    {
        if (string.Equals(mapName, DefaultMapName, StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceLoader();
        }

        // For a named .resw resource subtree, Windows App SDK requires the default
        // PRI path plus the map name. ResourceLoader(string) treats its argument as
        // the resource file/PRI path on current WinUI 3 localization guidance.
        return new ResourceLoader(ResourceLoader.GetDefaultResourceFilePath(), mapName);
    }

    private static string ResolveOverride(string? displayLanguage, bool followSystemLanguage)
    {
        if (followSystemLanguage || string.IsNullOrWhiteSpace(displayLanguage) ||
            string.Equals(displayLanguage, "System", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return SupportedLanguages.Contains(displayLanguage) ? displayLanguage : string.Empty;
    }
}
