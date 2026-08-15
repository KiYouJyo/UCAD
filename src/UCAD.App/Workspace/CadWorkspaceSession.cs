using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
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
        Viewport = new CadViewport(Document);
        CommandSession = new CommandSession(registry);
    }

    public int Ordinal { get; }

    public string DisplayName { get; }

    public CadDocument Document { get; }

    public CadViewport Viewport { get; }

    public CommandSession CommandSession { get; }

    public CadPoint? CommandBasePoint { get; set; }

    public CadPoint PointerWorldPosition { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public long SavedRevision { get; set; }

    public bool IsDirty => Document.Revision != SavedRevision;
}
