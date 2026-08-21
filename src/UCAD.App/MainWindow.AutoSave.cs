using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Services;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _autoSaveSubscribedSessions = [];
    private DispatcherTimer? _autoSaveTimer;
    private bool _autoSaveInitialized;
    private bool _autoSaveRunning;
    private bool _recoveryPromptShown;

    internal void EnsureAutoSaveInitialized()
    {
        if (_autoSaveInitialized) return;
        _autoSaveInitialized = true;
        RootLayout.Loaded += AutoSave_RootLoaded;
        DocumentTabs.SelectionChanged += AutoSave_DocumentTabsSelectionChanged;
        _settingsService.SettingsChanged += AutoSave_SettingsChanged;
    }

    private void AutoSave_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= AutoSave_RootLoaded;
        SubscribeAutoSaveSessions();
        ConfigureAutoSaveTimer();
        RootLayout.DispatcherQueue.TryEnqueue(async () => await OfferRecoveryAsync());
    }

    private void AutoSave_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SubscribeAutoSaveSessions();

    private void AutoSave_SettingsChanged(object? sender, EventArgs e) => ConfigureAutoSaveTimer();

    private void SubscribeAutoSaveSessions()
    {
        foreach (var session in _sessions.Values)
        {
            if (!_autoSaveSubscribedSessions.Add(session)) continue;
            session.Saved += AutoSave_SessionSaved;
        }
    }

    private async void AutoSave_SessionSaved(object? sender, EventArgs e)
    {
        if (sender is not CadWorkspaceSession session) return;
        try
        {
            await RecoveryService.Current.DeleteAsync(session.RecoveryId);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("RecoveryCleanupAfterSave", ex);
        }
    }

    private void ConfigureAutoSaveTimer()
    {
        _autoSaveTimer ??= new DispatcherTimer();
        _autoSaveTimer.Tick -= AutoSaveTimer_Tick;
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        _autoSaveTimer.Stop();

        if (!_settingsService.Settings.AutoSave) return;
        _autoSaveTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(_settingsService.Settings.AutoSaveIntervalMinutes, 1, 120));
        _autoSaveTimer.Start();
    }

    private async void AutoSaveTimer_Tick(object? sender, object e)
    {
        if (_autoSaveRunning || !_settingsService.Settings.AutoSave) return;
        _autoSaveRunning = true;
        try
        {
            SubscribeAutoSaveSessions();
            foreach (var session in _sessions.Values.ToArray())
            {
                if (!session.IsDirty) continue;
                await RecoveryService.Current.SaveAsync(
                    session.RecoveryId,
                    session.DisplayName,
                    session.FilePath,
                    session.Document);
            }
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("AutoSave", ex);
        }
        finally
        {
            _autoSaveRunning = false;
        }
    }

    private async Task OfferRecoveryAsync()
    {
        if (_recoveryPromptShown) return;
        _recoveryPromptShown = true;

        IReadOnlyList<RecoveryCandidate> candidates;
        try
        {
            candidates = await RecoveryService.Current.GetCandidatesAsync();
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("RecoveryScan", ex);
            return;
        }
        if (candidates.Count == 0) return;

        var newest = candidates[0];
        var result = await new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = RecoveryText("Title"),
            Content = string.Format(
                RecoveryText("Content"),
                newest.DisplayName,
                newest.UpdatedUtc.ToLocalTime().ToString("g"),
                candidates.Count),
            PrimaryButtonText = RecoveryText("Restore"),
            SecondaryButtonText = RecoveryText("DiscardAll"),
            CloseButtonText = RecoveryText("Later"),
            DefaultButton = ContentDialogButton.Primary
        }.ShowAsync();

        if (result == ContentDialogResult.Secondary)
        {
            await RecoveryService.Current.DeleteAllAsync();
            return;
        }
        if (result != ContentDialogResult.Primary) return;

        try
        {
            var document = await RecoveryService.Current.LoadAsync(newest);
            var session = CreateWorkspaceForFile(document, newest.DisplayName, newest.SourcePath);
            session.MarkRecovered(newest.DisplayName, newest.SourcePath);
            var tab = FindTab(session);
            if (tab is not null) UpdateTabHeader(tab, session);
            SetSessionStatus(session, RecoveryText("RestoredStatus"));

            // Move the recovered state into this runtime session's identity immediately,
            // then retire the old crash record so a subsequent save can clean it normally.
            await RecoveryService.Current.SaveAsync(
                session.RecoveryId,
                session.DisplayName,
                session.FilePath,
                session.Document);
            await RecoveryService.Current.DeleteAsync(newest.Id);
            SubscribeAutoSaveSessions();
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("RecoveryRestore", ex);
            await ShowFileMessageAsync(RecoveryText("FailedTitle"), ex.Message);
        }
    }

    private static string RecoveryText(string key)
    {
        var language = LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "Title" => ja ? "自動保存データを復元しますか？" : en ? "Restore autosaved drawing?" : "是否恢复自动保存的图纸？",
            "Content" => ja ? "「{0}」の自動保存 ({1}) が見つかりました。復元候補: {2} 件。" : en ? "An autosave for {0} from {1} was found. Recovery candidates: {2}." : "发现“{0}”在 {1} 的自动保存。共有 {2} 个恢复候选。",
            "Restore" => ja ? "復元" : en ? "Restore" : "恢复",
            "DiscardAll" => ja ? "すべて破棄" : en ? "Discard all" : "全部丢弃",
            "Later" => ja ? "後で" : en ? "Later" : "稍后",
            "RestoredStatus" => ja ? "自動保存から復元しました。保存して変更を確定してください。" : en ? "Recovered from autosave. Save to commit the recovered changes." : "已从自动保存恢复，请保存以确认恢复内容。",
            "FailedTitle" => ja ? "復元できません" : en ? "Couldn’t restore drawing" : "无法恢复图纸",
            _ => key
        };
    }
}
