using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Services;
using UCAD.Views;

namespace UCAD.Workspace;

public sealed class CadWorkspaceSession
{
    public CadWorkspaceSession(int ordinal, string displayName, CommandRegistry registry, CadDocument? document = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Ordinal = ordinal;
        DisplayName = displayName;
        Document = document ?? new CadDocument();
        Interaction = new CadInteractionState(Document);
        ApplyDraftingDefaults(SettingsService.Current.Settings);
        Viewport = new CadViewport(Document, Interaction);
        CommandSession = new CommandSession(registry);
        SavedRevision = Document.Revision;
    }

    public int Ordinal { get; }

    public string DisplayName { get; private set; }

    public string? FilePath { get; private set; }

    public CadDocument Document { get; }

    public CadInteractionState Interaction { get; }

    public CadViewport Viewport { get; }

    public CommandSession CommandSession { get; }

    public CadPoint? CommandBasePoint { get; set; }

    public CadPoint PointerWorldPosition { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public long SavedRevision { get; private set; }

    public bool IsDirty => Document.Revision != SavedRevision;

    public bool HasFilePath => !string.IsNullOrWhiteSpace(FilePath);

    public void UpdateDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName;
        }
    }

    public void MarkOpened(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        DisplayName = Path.GetFileName(FilePath);
        SavedRevision = Document.Revision;
    }

    public void MarkSaved(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        DisplayName = Path.GetFileName(FilePath);
        SavedRevision = Document.Revision;
    }

    public void MarkSaved()
    {
        if (!HasFilePath)
        {
            throw new InvalidOperationException("Cannot mark an untitled drawing as saved without a file path.");
        }
        SavedRevision = Document.Revision;
    }

    private void ApplyDraftingDefaults(AppSettings settings)
    {
        Interaction.ObjectSnapEnabled = settings.DefaultObjectSnap;
        Interaction.ObjectSnapModes = settings.DefaultSnapTypes switch
        {
            "EndpointMidpoint" => ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint,
            _ => ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint | ObjectSnapMode.Center | ObjectSnapMode.Intersection
        };
        Interaction.OrthoEnabled = settings.DefaultOrtho;
    }
}
