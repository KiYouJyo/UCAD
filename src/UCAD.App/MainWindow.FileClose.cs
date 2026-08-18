using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        if (!_sessions.TryGetValue(tab, out var session) || !session.IsDirty || !_settingsService.Settings.ConfirmUnsaved)
            return await TryCloseTabAsync(tab);

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
                return await TryCloseTabAsync(tab);
            }
            finally
            {
                _settingsService.Settings.ConfirmUnsaved = original;
            }
        }

        return await TryCloseTabAsync(tab);
    }

    private static string FileCloseText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
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
