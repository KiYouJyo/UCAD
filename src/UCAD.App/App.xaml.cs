using Microsoft.UI.Xaml;
using System.Text;
using UCAD.Services;

namespace UCAD;

public partial class App : Application
{
    private Window? _window;

    internal static string StartupLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UCAD",
        "Logs",
        "startup.log");

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        SettingsService.Current.SettingsChanged += SettingsService_SettingsChanged;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            WriteStartupEvent("OnLaunched begin");
            LocalizationService.Current.ApplyFromSettings();
            var mainWindow = new MainWindow();
            mainWindow.RefreshLocalization();
            mainWindow.EnsureInteractionUiInitialized();
            mainWindow.EnsureAuthoringUiInitialized();
            mainWindow.EnsureFileUiInitialized();
            mainWindow.EnsureRecentFilesInitialized();
            mainWindow.EnsureFileCloseIntegration();
            mainWindow.ScheduleLocalizationSmoke();
            mainWindow.ScheduleInteractionSmoke();
            mainWindow.ScheduleAuthoringSmoke();
            _window = mainWindow;
            WriteStartupEvent("MainWindow constructed and localized");
            _window.Activate();
            WriteStartupEvent("MainWindow activated");
        }
        catch (Exception ex)
        {
            WriteStartupFailure("OnLaunched", ex);
            throw;
        }
    }

    private void SettingsService_SettingsChanged(object? sender, EventArgs e)
    {
        var localization = LocalizationService.Current;
        if (localization.IsSettingsLanguageApplied) return;
        if (_window is MainWindow mainWindow) mainWindow.ApplyLiveLocalizationFromSettings();
    }

    private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) =>
        WriteStartupFailure("Xaml.UnhandledException", e.Exception);

    private static void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) WriteStartupFailure("AppDomain.UnhandledException", ex);
        else WriteStartupEvent($"AppDomain.UnhandledException: {e.ExceptionObject}");
    }

    internal static void WriteStartupFailure(string stage, Exception ex)
    {
        var builder = new StringBuilder()
            .AppendLine($"[{DateTimeOffset.Now:O}] {stage}")
            .AppendLine(ex.ToString())
            .AppendLine();
        AppendStartupLog(builder.ToString());
    }

    internal static void WriteStartupEvent(string message) =>
        AppendStartupLog($"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");

    private static void AppendStartupLog(string text)
    {
        try
        {
            var path = StartupLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, text, Encoding.UTF8);
        }
        catch
        {
            // Startup diagnostics must never become a second startup failure.
        }
    }
}
