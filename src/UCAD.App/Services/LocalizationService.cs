using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace UCAD.Services;

/// <summary>
/// Owns UCAD's runtime language context. ResourceLoader instances capture a resource
/// context, so they must be recreated after PrimaryLanguageOverride changes.
/// </summary>
public sealed class LocalizationService
{
    private const string DefaultMapName = "Resources";
    private const string V039MapName = "UcadV039";
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

    /// <summary>
    /// Resolves a key from the default Resources map first, then from the v0.3.9
    /// Start/Settings map. This keeps existing shell call sites stable.
    /// </summary>
    public string GetString(string key)
    {
        var value = GetStringFromMap(key, DefaultMapName);
        return string.IsNullOrWhiteSpace(value) ? GetStringFromMap(key, V039MapName) : value;
    }

    public string GetV039String(string key) => GetStringFromMap(key, V039MapName);

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

        // Important: the one-argument ResourceLoader(string) overload is a PRI file
        // path constructor in current Windows App SDK guidance. A named .resw subtree
        // must use the two-argument constructor with the default PRI file path.
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
