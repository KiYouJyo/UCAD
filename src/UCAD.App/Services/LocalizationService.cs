using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace UCAD.Services;

/// <summary>
/// Owns UCAD's runtime language context. Language selection is expressed through an
/// explicit MRT Core ResourceContext instead of mutating the process-global language
/// override. Existing WinUI elements are then refreshed in place by MainWindow.
/// </summary>
public sealed class LocalizationService
{
    private const string DefaultMapName = "Resources";
    private const string V039MapName = "UcadV039";
    private const string ShellLiveMapName = "ShellLive";
    private const string LiveReloadNoteKey = "Settings_Language_ReloadNote";
    private const string DraftingInteractionNoteKey = "Settings_Drafting_PendingNote";
    private const string CoreSnapOptionKey = "Settings_Option_SnapCore";
    private static readonly string[] SupportedLanguages = ["zh-CN", "ja-JP", "en-US"];

    private readonly Dictionary<string, ResourceMap?> _maps = new(StringComparer.OrdinalIgnoreCase);
    private ResourceManager? _resourceManager;
    private ResourceContext? _resourceContext;

    private LocalizationService()
    {
        CurrentLanguageTag = ResolveSystemLanguage();
    }

    public static LocalizationService Current { get; } = new();

    public string CurrentLanguageTag { get; private set; }

    public int Generation { get; private set; }

    public bool IsSettingsLanguageApplied
    {
        get
        {
            var settings = SettingsService.Current.Settings;
            return string.Equals(
                CurrentLanguageTag,
                ResolveLanguage(settings.DisplayLanguage, settings.FollowSystemLanguage),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool ApplyFromSettings(bool writeLog = true)
    {
        var settings = SettingsService.Current.Settings;
        return ApplyLanguagePreference(settings.DisplayLanguage, settings.FollowSystemLanguage, writeLog);
    }

    public bool ApplyLanguagePreference(string? displayLanguage, bool followSystemLanguage, bool writeLog = true)
    {
        var language = ResolveLanguage(displayLanguage, followSystemLanguage);

        try
        {
            EnsureResourceInfrastructure();
            var context = _resourceManager!.CreateResourceContext();
            context.QualifierValues[KnownResourceQualifierName.Language] = language;
            _resourceContext = context;
            CurrentLanguageTag = language;
            _maps.Clear();
            Generation++;

            if (writeLog)
            {
                App.WriteStartupEvent(followSystemLanguage
                    ? $"Display language applied live from system preference: {language}"
                    : $"Display language applied live: {language}");
            }
            return true;
        }
        catch (Exception ex)
        {
            if (writeLog)
            {
                App.WriteStartupFailure($"ApplyLanguage:{language}", ex);
            }
            return false;
        }
    }

    public string GetString(string key)
    {
        var value = GetStringFromMap(key, DefaultMapName);
        return string.IsNullOrWhiteSpace(value) ? GetV039String(key) : value;
    }

    public string GetShellString(string key) => GetStringFromMap(key, ShellLiveMapName);

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

        // The key predates v0.4.0 and is kept for Settings layout compatibility.
        // Its runtime message now reflects the real drafting interaction implementation.
        if (string.Equals(key, DraftingInteractionNoteKey, StringComparison.Ordinal))
        {
            return CurrentLanguageTag switch
            {
                "ja-JP" => "v0.4.0 では OSNAP と直交が実際の作図入力へ接続されています。ここで設定した既定値は新しい図面に適用され、作図中は F3 / F8 でも切り替えられます。",
                "en-US" => "In v0.4.0, OSNAP and Ortho are connected to real drawing input. These defaults apply to new drawings and can be toggled during drafting with F3 / F8.",
                _ => "v0.4.0 已将对象捕捉与正交接入真实绘图输入；此处默认值用于新图纸，绘图时也可通过 F3 / F8 随时切换。"
            };
        }

        // v0.4.0 promotes Center to the complete foundational OSNAP set. Keep the
        // historic resource ID so saved Settings values and the Figma contract remain stable.
        if (string.Equals(key, CoreSnapOptionKey, StringComparison.Ordinal))
        {
            return CurrentLanguageTag switch
            {
                "ja-JP" => "端点 / 中点 / 中心 / 交点",
                "en-US" => "Endpoint / Midpoint / Center / Intersection",
                _ => "端点 / 中点 / 圆心 / 交点"
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
            EnsureResourceInfrastructure();
            EnsureContext();

            var map = GetMap(mapName);
            var candidate = map?.TryGetValue(key, _resourceContext!);
            if (candidate is not null)
            {
                return candidate.ValueAsString ?? string.Empty;
            }

            // Be tolerant of PRI layouts where the named .resw map is addressable as
            // a path from the main map rather than as a directly returned subtree.
            candidate = _resourceManager!.MainResourceMap.TryGetValue($"{mapName}/{key}", _resourceContext!);
            return candidate?.ValueAsString ?? string.Empty;
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure($"Localization:{mapName}:{key}", ex);
            return string.Empty;
        }
    }

    private void EnsureResourceInfrastructure()
    {
        _resourceManager ??= new ResourceManager();
    }

    private void EnsureContext()
    {
        if (_resourceContext is not null)
        {
            return;
        }

        var context = _resourceManager!.CreateResourceContext();
        context.QualifierValues[KnownResourceQualifierName.Language] = CurrentLanguageTag;
        _resourceContext = context;
    }

    private ResourceMap? GetMap(string mapName)
    {
        if (_maps.TryGetValue(mapName, out var cached))
        {
            return cached;
        }

        var map = _resourceManager!.MainResourceMap.TryGetSubtree(mapName);
        _maps[mapName] = map;
        return map;
    }

    private static string ResolveLanguage(string? displayLanguage, bool followSystemLanguage)
    {
        if (!followSystemLanguage && !string.IsNullOrWhiteSpace(displayLanguage) &&
            !string.Equals(displayLanguage, "System", StringComparison.OrdinalIgnoreCase))
        {
            var explicitLanguage = NormalizeSupportedLanguage(displayLanguage);
            if (explicitLanguage is not null)
            {
                return explicitLanguage;
            }
        }

        return ResolveSystemLanguage();
    }

    private static string ResolveSystemLanguage()
    {
        try
        {
            foreach (var language in Windows.Globalization.ApplicationLanguages.Languages)
            {
                var supported = NormalizeSupportedLanguage(language);
                if (supported is not null)
                {
                    return supported;
                }
            }
        }
        catch
        {
            // Fall through to CurrentUICulture for unpackaged/test hosts.
        }

        var culture = NormalizeSupportedLanguage(CultureInfo.CurrentUICulture.Name);
        return culture ?? "en-US";
    }

    private static string? NormalizeSupportedLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        foreach (var supported in SupportedLanguages)
        {
            if (string.Equals(language, supported, StringComparison.OrdinalIgnoreCase))
            {
                return supported;
            }
        }

        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return null;
    }
}
