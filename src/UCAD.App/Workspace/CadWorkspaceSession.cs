using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Views;

namespace UCAD.Workspace;

public sealed class CadWorkspaceSession
{
    public CadWorkspaceSession(int ordinal, string displayName, CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Ordinal = ordinal;
        DisplayName = displayName;
        Document = new CadDocument();
        Interaction = new CadInteractionState(Document);
        Viewport = new CadViewport(Document, Interaction);
        CommandSession = new CommandSession(registry);
    }

    public int Ordinal { get; }

    public string DisplayName { get; private set; }

    public CadDocument Document { get; }

    public CadInteractionState Interaction { get; }

    public CadViewport Viewport { get; }

    public CommandSession CommandSession { get; }

    public CadPoint? CommandBasePoint { get; set; }

    public CadPoint PointerWorldPosition { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public long SavedRevision { get; set; }

    public bool IsDirty => Document.Revision != SavedRevision;

    public void UpdateDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName;
        }
    }
}
