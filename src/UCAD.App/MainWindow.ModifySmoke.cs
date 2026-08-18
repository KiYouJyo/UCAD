using Microsoft.UI.Xaml.Controls;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private void RunV05ModifyInteractionSmoke()
    {
        CreateNewWorkspace();
        var session = ActiveSession ?? throw new InvalidOperationException("Modify smoke could not create a Drawing workspace.");
        EnsureSessionInteractionSubscribed(session);

        var eraseButton = ModifyToolShelf.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Tag as string, "ERASE", StringComparison.Ordinal));
        if (eraseButton is null || !eraseButton.IsHitTestVisible || !eraseButton.IsEnabled || eraseButton.Opacity < 0.99)
        {
            throw new InvalidOperationException("Modify smoke visible ERASE control is not available.");
        }

        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var circle = new CircleEntity(new CadPoint(20, 0), 5);
        session.Document.Add(line);
        session.Document.Add(circle);
        session.Interaction.Selection.Replace(line.Id);

        StartModifySmokeCommand(session, "MOVE");
        OnModifyPointAccepted(session, new CadPoint(0, 0));
        OnModifyPointAccepted(session, new CadPoint(5, 3));
        var moved = RequireLine(session, line.Id);
        AssertModifyClose(new CadPoint(5, 3), moved.Start, "MOVE start");
        AssertModifyClose(new CadPoint(15, 3), moved.End, "MOVE end");
        if (!session.Document.Undo()) throw new InvalidOperationException("Modify smoke MOVE Undo failed.");

        StartModifySmokeCommand(session, "COPY");
        OnModifyPointAccepted(session, new CadPoint(0, 0));
        OnModifyPointAccepted(session, new CadPoint(0, 10));
        if (session.Document.Entities.Count != 3 || session.Document.Entities.Count(entity => entity is LineEntity) != 2)
            throw new InvalidOperationException("Modify smoke COPY failed.");
        if (!session.Document.Undo()) throw new InvalidOperationException("Modify smoke COPY Undo failed.");

        StartModifySmokeCommand(session, "ROTATE");
        OnModifyPointAccepted(session, new CadPoint(0, 0));
        OnModifyPointAccepted(session, new CadPoint(0, 1));
        var rotated = RequireLine(session, line.Id);
        AssertModifyClose(new CadPoint(0, 10), rotated.End, "ROTATE end");
        if (!session.Document.Undo()) throw new InvalidOperationException("Modify smoke ROTATE Undo failed.");

        StartModifySmokeCommand(session, "SCALE");
        OnModifyPointAccepted(session, new CadPoint(0, 0));
        SubmitModifyCommandLine(session, "2");
        var scaled = RequireLine(session, line.Id);
        AssertModifyClose(new CadPoint(20, 0), scaled.End, "SCALE end");
        if (!session.Document.Undo()) throw new InvalidOperationException("Modify smoke SCALE Undo failed.");

        StartModifySmokeCommand(session, "MIRROR");
        OnModifyPointAccepted(session, new CadPoint(0, 0));
        OnModifyPointAccepted(session, new CadPoint(0, 10));
        SubmitModifyCommandLine(session, string.Empty);
        var mirrored = session.Document.Entities
            .OfType<LineEntity>()
            .FirstOrDefault(candidate => candidate.Id != line.Id && Math.Abs(candidate.End.X + 10) < 1e-7);
        if (mirrored is null) throw new InvalidOperationException("Modify smoke MIRROR failed.");
        if (!session.Document.Undo()) throw new InvalidOperationException("Modify smoke MIRROR Undo failed.");

        StartModifySmokeCommand(session, "OFFSET");
        SubmitModifyCommandLine(session, "2");
        OnModifyEntityPicked(session, line.Id, new CadPoint(5, 0));
        OnModifyPointAccepted(session, new CadPoint(5, 5));
        var offset = session.Document.Entities
            .OfType<LineEntity>()
            .FirstOrDefault(candidate => candidate.Id != line.Id && Math.Abs(candidate.Start.Y - 2) < 1e-7);
        if (offset is null) throw new InvalidOperationException("Modify smoke OFFSET failed.");
        if (!session.Document.Undo()) throw new InvalidOperationException("Modify smoke OFFSET Undo failed.");

        var trimLeft = new LineEntity(new CadPoint(3, -5), new CadPoint(3, 5));
        var trimRight = new LineEntity(new CadPoint(7, -5), new CadPoint(7, 5));
        session.Document.Add(trimLeft);
        session.Document.Add(trimRight);
        StartModifySmokeCommand(session, "TRIM");
        OnModifyEntityPicked(session, line.Id, new CadPoint(5, 0));
        SubmitModifyCommandLine(session, string.Empty);
        var trimmedPieces = session.Document.Entities.OfType<LineEntity>()
            .Where(candidate => Math.Abs(candidate.Start.Y) < 1e-7 && Math.Abs(candidate.End.Y) < 1e-7)
            .ToArray();
        if (trimmedPieces.Length < 2 || trimmedPieces.Any(piece =>
            Math.Min(piece.Start.X, piece.End.X) < 3 - 1e-7 && Math.Max(piece.Start.X, piece.End.X) > 7 + 1e-7))
        {
            throw new InvalidOperationException("Modify smoke TRIM failed.");
        }
        if (!session.Document.Undo()) throw new InvalidOperationException("Modify smoke TRIM Undo failed.");

        var extendTarget = new LineEntity(new CadPoint(0, 20), new CadPoint(5, 20));
        var extendBoundary = new LineEntity(new CadPoint(10, 15), new CadPoint(10, 25));
        session.Document.Add(extendTarget);
        session.Document.Add(extendBoundary);
        StartModifySmokeCommand(session, "EXTEND");
        OnModifyEntityPicked(session, extendTarget.Id, new CadPoint(5, 20));
        SubmitModifyCommandLine(session, string.Empty);
        var extended = RequireLine(session, extendTarget.Id);
        AssertModifyClose(new CadPoint(10, 20), extended.End, "EXTEND end");

        App.WriteStartupEvent("Modify smoke: visible ERASE + MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND initialized");
    }

    private void StartModifySmokeCommand(CadWorkspaceSession session, string token)
    {
        // Use the exact production dispatch path instead of calling CommandSession.Start
        // directly; this catches regressions between registry resolution, shell dispatch,
        // CommandSession lifecycle, and the v0.5 Modify controller.
        StartCommand(session, token);
        if (session.CommandSession.ActiveCommand?.Category != CadCommandCategory.Modify ||
            !string.Equals(session.CommandSession.ActiveCommand.Name, token, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Modify smoke could not start {token} through production dispatch.");
        }
    }

    private static LineEntity RequireLine(CadWorkspaceSession session, Guid id) =>
        session.Document.Entities.OfType<LineEntity>().FirstOrDefault(entity => entity.Id == id)
        ?? throw new InvalidOperationException($"Modify smoke line {id} was not found.");

    private static void AssertModifyClose(CadPoint expected, CadPoint actual, string scope)
    {
        if ((actual - expected).Length > 1e-7)
        {
            throw new InvalidOperationException($"Modify smoke {scope} mismatch. Expected {expected}; actual {actual}.");
        }
    }
}
