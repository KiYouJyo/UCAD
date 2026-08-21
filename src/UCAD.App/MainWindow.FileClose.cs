using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Services;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _fileCloseIntegrated;

    internal void EnsureFileCloseIntegration()
    {
        if (_fileCloseIntegrated) return;
        _fileCloseIntegrated = true;

        // Replace the v0.3 discard-only close flow with Save / Discard / Cancel while
        // reusing TryCloseTabAsync for the actual tab/session teardown.
        DocumentTabs.TabCloseRequested -= DocumentTabs_TabCloseRequested;
        DocumentTabs.TabCloseRequested += FileClose_TabCloseRequested;
        CloseDrawingMenuItem.Click -= CloseDrawingMenuItem_Click;
        CloseDrawingMenuItem.Click += FileClose_CloseDrawingMenuItem_Click;
    }

    private async void FileClose_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) =>
        await TryCloseWithSaveAsync(args.Tab);

    private async void FileClose_CloseDrawingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is TabViewItem tab) await TryCloseWithSaveAsync(tab);
    }

    private async Task<bool> TryCloseWithSaveAsync(TabViewItem tab)
    {
        _sessions.TryGetValue(tab, out var session);
        if (session is null || !session.IsDirty || !_settingsService.Settings.ConfirmUnsaved)
        {
            var closed = await TryCloseTabAsync(tab);
            if (closed && session is not null) await DeleteRecoveryAfterIntentionalCloseAsync(session);
            return closed;
        }

        var result = await new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = FileCloseText("Title"),
            Content = string.Format(FileCloseText("Content"), session.DisplayName),
            PrimaryButtonText = FileCloseText("Save"),
            SecondaryButtonText = FileCloseText("Discard"),
            CloseButtonText = FileCloseText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        }.ShowAsync();

        if (result == ContentDialogResult.None) return false;
        if (result == ContentDialogResult.Primary && !await SaveSessionAsync(session, saveAs: false)) return false;

        if (result == ContentDialogResult.Secondary)
        {
            var original = _settingsService.Settings.ConfirmUnsaved;
            _settingsService.Settings.ConfirmUnsaved = false;
            try
            {
                var closed = await TryCloseTabAsync(tab);
                if (closed) await DeleteRecoveryAfterIntentionalCloseAsync(session);
                return closed;
            }
            finally
            {
                _settingsService.Settings.ConfirmUnsaved = original;
            }
        }

        var savedClose = await TryCloseTabAsync(tab);
        if (savedClose) await DeleteRecoveryAfterIntentionalCloseAsync(session);
        return savedClose;
    }

    private static async Task DeleteRecoveryAfterIntentionalCloseAsync(CadWorkspaceSession session)
    {
        try
        {
            await RecoveryService.Current.DeleteAsync(session.RecoveryId);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("RecoveryCleanupAfterClose", ex);
        }
    }

    private static string FileCloseText(string key)
    {
        var language = LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "Title" => ja ? "変更を保存しますか？" : en ? "Save changes?" : "是否保存修改？",
            "Content" => ja ? "「{0}」には保存されていない変更があります。" : en ? "{0} has unsaved changes." : "“{0}”包含未保存的修改。",
            "Save" => ja ? "保存" : en ? "Save" : "保存",
            "Discard" => ja ? "保存しない" : en ? "Don't save" : "不保存",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            _ => key
        };
    }
}
