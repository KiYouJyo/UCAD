using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Core.IO;
using Windows.Storage.Pickers;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _autoCadMigrationUiInitialized;
    private MenuFlyoutItem? _autoCadMigrationImportItem;

    private void EnsureAutoCadMigrationUi()
    {
        if (_autoCadMigrationUiInitialized) return;
        _autoCadMigrationUiInitialized = true;
        if (RootLayout.IsLoaded) RootLayout.DispatcherQueue.TryEnqueue(ConfigureAutoCadMigrationMenu);
        else RootLayout.Loaded += AutoCadMigration_RootLoaded;
    }

    private void AutoCadMigration_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= AutoCadMigration_RootLoaded;
        // FileIO also rebuilds the File Menu during Loaded. Enqueue so migration entries
        // are inserted after that authoritative menu rebuild rather than being cleared by it.
        RootLayout.DispatcherQueue.TryEnqueue(ConfigureAutoCadMigrationMenu);
    }

    private void ConfigureAutoCadMigrationMenu()
    {
        if (_autoCadMigrationImportItem is not null || FileMenuButton.Flyout is not MenuFlyout menu) return;
        _autoCadMigrationImportItem = new MenuFlyoutItem { Text = MigrationText("Import") };
        _autoCadMigrationImportItem.Click += AutoCadMigrationImportItem_Click;

        var closeIndex = menu.Items.IndexOf(CloseDrawingMenuItem);
        if (closeIndex < 0) closeIndex = menu.Items.Count;
        menu.Items.Insert(closeIndex, _autoCadMigrationImportItem);
    }

    private async void AutoCadMigrationImportItem_Click(object sender, RoutedEventArgs e) =>
        await ImportAutoCadMigrationResourceAsync();

    private async Task ImportAutoCadMigrationResourceAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        foreach (var extension in CadAcadFileFormatRegistry.MigratableAutoCadFormats
                     .Select(format => format.Extension)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            picker.FileTypeFilter.Add(extension);
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var extension = Path.GetExtension(file.Path).ToLowerInvariant();
            var bytes = await File.ReadAllBytesAsync(file.Path);
            var summary = await BuildAutoCadMigrationSummaryAsync(extension, bytes, file.Path);
            if (!string.IsNullOrWhiteSpace(summary))
                await ShowFileMessageAsync(MigrationText("Title"), summary);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("AutoCadMigration", ex);
            await ShowFileMessageAsync(MigrationText("Failed"), ex.Message);
        }
    }

    private async Task<string> BuildAutoCadMigrationSummaryAsync(string extension, byte[] bytes, string path)
    {
        if (extension == ".dwfx")
        {
            var imported = CadDwfxCodec.Import(bytes);
            var session = CreateWorkspaceForFile(imported.Document, Path.GetFileName(path), nativeFilePath: null);
            SetSessionStatus(session, MigrationText("DwfxOpened"));
            var details = $"DWFx FixedPage: {imported.Document.Entities.Count} editable vector entities";
            if (imported.Warnings.Count > 0) details += Environment.NewLine + string.Join(Environment.NewLine, imported.Warnings.Take(8));
            return details;
        }

        if (extension == ".scr")
        {
            var script = CadAcadEcosystemResourceCodec.ParseScript(DecodeText(bytes));
            var result = RunSafeAutoCadScript(script);
            return $"SCR: {script.Statements.Count} statements; {result.Executed} executed; {result.Skipped} require unsupported/interactive input.";
        }

        if (extension is ".lsp" or ".mnl")
        {
            var report = CadAcadEcosystemResourceCodec.AnalyzeLispSource(DecodeText(bytes), extension);
            var functions = report.DefinedFunctions.Count == 0 ? "—" : string.Join(", ", report.DefinedFunctions.Take(12));
            var commands = report.CommandInvocations.Count == 0 ? "—" : string.Join(", ", report.CommandInvocations.Take(12));
            return $"{extension.ToUpperInvariant()} source migration\nFunctions: {functions}\nCommand calls: {commands}\nNo Lisp code was executed.";
        }

        if (extension == ".cuix")
        {
            var migration = CadAcadEcosystemResourceCodec.ImportCuix(bytes);
            return $"CUIX: {migration.Entries.Count} package entries; {migration.CommandMetadata.Count} command/UI metadata values.\nEmbedded code/macros were not executed.";
        }

        if (extension == ".pat")
        {
            var patterns = CadAcadTextResourceCodec.ParsePat(DecodeText(bytes));
            return $"PAT: {patterns.Count} hatch pattern definitions parsed.";
        }
        if (extension == ".lin")
        {
            var linetypes = CadAcadTextResourceCodec.ParseLin(DecodeText(bytes));
            return $"LIN: {linetypes.Count} linetype definitions parsed.";
        }
        if (extension == ".pgp")
        {
            var aliases = CadAcadTextResourceCodec.ParsePgpAliases(DecodeText(bytes));
            return $"PGP: {aliases.Count} safe command aliases parsed; external-process records were ignored.";
        }

        if (extension is ".fmp" or ".unt" or ".cfg" or ".arg" or ".rx" or ".dcl" or ".cui" or ".mnu" or ".mns" or ".atc" or ".dsd" or ".bp3")
        {
            var resource = CadAcadEcosystemResourceCodec.ImportTextResource(DecodeText(bytes), extension);
            var entries = resource.Sections.Sum(section => section.Value.Count);
            return $"{extension.ToUpperInvariant()}: {resource.Sections.Count} sections / {entries} migration metadata entries parsed and source preserved.";
        }

        var opaque = CadAcadEcosystemResourceCodec.ImportLosslessBinary(bytes, extension);
        var warning = opaque.Warnings.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, opaque.Warnings.Take(6));
        return $"{extension.ToUpperInvariant()}: exact {bytes.Length:N0}-byte migration package preserved.\nSHA-256 {opaque.Metadata["sha256"]}{warning}";
    }

    private CadAcadScriptRunResult RunSafeAutoCadScript(CadAcadScript script)
    {
        var session = ActiveSession;
        if (session is null)
        {
            CreateNewWorkspace();
            session = ActiveSession;
        }
        if (session is null) return new CadAcadScriptRunResult(0, script.Statements.Count);

        var executed = 0;
        var skipped = 0;
        foreach (var statement in script.Statements)
        {
            if (!_commandRegistry.TryResolve(statement.Command, out var command) || command is null)
            {
                skipped++;
                continue;
            }

            // Immediate commands can safely use the production dispatcher. Geometry commands
            // are accepted only when all point inputs are present on the same SCR statement.
            if (statement.Arguments.Count == 0 && command.Name is "UNDO" or "REDO" or "ERASE" or "CLEAR" or "RESETVIEW")
            {
                StartToolbarCommand(command.Name);
                executed++;
                continue;
            }

            if (command.Name is "LINE" or "PLINE" or "RECTANGLE")
            {
                var required = command.Name == "RECTANGLE" ? 2 : 2;
                if (statement.Arguments.Count < required) { skipped++; continue; }
                StartToolbarCommand(command.Name);
                var accepted = true;
                foreach (var token in statement.Arguments)
                {
                    if (!TryResolvePointInput(session, token, out var point) || !session.Viewport.SubmitDrawingPoint(point))
                    {
                        accepted = false;
                        break;
                    }
                }
                if (command.Name == "PLINE" && accepted) session.Viewport.CompleteDrawingCommand();
                if (!accepted)
                {
                    session.Viewport.CancelDrawingCommand();
                    session.CommandSession.Cancel();
                    skipped++;
                }
                else executed++;
                continue;
            }

            skipped++;
        }
        return new CadAcadScriptRunResult(executed, skipped);
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        try { return Encoding.UTF8.GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.GetEncoding(1252).GetString(bytes); }
    }

    private string MigrationText(string key)
    {
        var language = LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "Import" => ja ? "AutoCAD リソース/カスタマイズを移行…" : en ? "Migrate AutoCAD resource/customization…" : "迁移 AutoCAD 资源/自定义…",
            "Title" => ja ? "AutoCAD 移行結果" : en ? "AutoCAD migration result" : "AutoCAD 迁移结果",
            "Failed" => ja ? "AutoCAD 移行に失敗しました" : en ? "AutoCAD migration failed" : "AutoCAD 迁移失败",
            "DwfxOpened" => ja ? "DWFx 固定ページを読み込みました" : en ? "DWFx fixed page imported" : "已导入 DWFx 固定页面",
            _ => key
        };
    }

    private sealed record CadAcadScriptRunResult(int Executed, int Skipped);
}
