using Microsoft.UI.Xaml.Input;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;
using UCAD.Workspace;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly Dictionary<CadWorkspaceSession, ModifyCommandContext> _modifyContexts = [];
    private readonly HashSet<CadWorkspaceSession> _modifyViewportSubscriptions = [];
    private bool _modifyCommandInputHookInstalled;

    /// <summary>
    /// Called from the shared CommandSession boundary. Returns true when the active
    /// command belongs to v0.5 Modify and the controller owns its interaction state.
    /// </summary>
    private bool HandleModifyCommandSessionChanged(CadWorkspaceSession session)
    {
        var active = session.CommandSession.ActiveCommand;
        if (active?.Category == CadCommandCategory.Modify)
        {
            EnsureModifyInputRouting();
            EnsureModifyViewportSubscribed(session);
            if (!_modifyContexts.TryGetValue(session, out var existing) ||
                !string.Equals(existing.CommandName, active.Name, StringComparison.Ordinal))
            {
                BeginModifyCommand(session, active);
            }
            return true;
        }

        if (_modifyContexts.Remove(session))
        {
            session.Viewport.CancelModifyInput();
            session.CommandBasePoint = null;
        }
        return false;
    }

    private void EnsureModifyInputRouting()
    {
        if (_modifyCommandInputHookInstalled)
        {
            return;
        }
        _modifyCommandInputHookInstalled = true;

        // The XAML handler remains the single implementation for drawing/general
        // commands. Replace it with a thin gate only after the first Modify command
        // starts, then delegate unchanged behavior whenever Modify is not active.
        CommandInput.KeyDown -= CommandInput_KeyDown;
        CommandInput.KeyDown += ModifyAwareCommandInput_KeyDown;
    }

    private void EnsureModifyViewportSubscribed(CadWorkspaceSession session)
    {
        if (!_modifyViewportSubscriptions.Add(session))
        {
            return;
        }

        session.Viewport.ModifyPointAccepted += point => OnModifyPointAccepted(session, point);
        session.Viewport.ModifyEntityPicked += (id, pickPoint) => OnModifyEntityPicked(session, id, pickPoint);
    }

    private void BeginModifyCommand(CadWorkspaceSession session, CadCommandDefinition command)
    {
        session.Viewport.CancelModifyInput();
        session.CommandBasePoint = null;
        var context = new ModifyCommandContext(command.Name);
        _modifyContexts[session] = context;

        switch (command.Name)
        {
            case "MOVE":
            case "COPY":
            case "ROTATE":
            case "SCALE":
            case "MIRROR":
                BeginSelectionBackedModify(session, context);
                break;

            case "OFFSET":
                context.Phase = ModifyPhase.OffsetDistance;
                SetSessionStatus(session, ShellString("ModifyOffsetDistance"));
                CommandInput.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                break;

            case "TRIM":
                context.Phase = ModifyPhase.TrimPick;
                session.Viewport.BeginModifyEntityPickInput();
                SetSessionStatus(session, ShellString("ModifyTrimPick"));
                break;

            case "EXTEND":
                context.Phase = ModifyPhase.ExtendPick;
                session.Viewport.BeginModifyEntityPickInput();
                SetSessionStatus(session, ShellString("ModifyExtendPick"));
                break;
        }
    }

    private void BeginSelectionBackedModify(CadWorkspaceSession session, ModifyCommandContext context)
    {
        if (session.Interaction.Selection.IsEmpty)
        {
            context.Phase = ModifyPhase.SelectObjects;
            SetSessionStatus(session, ShellString("ModifySelectObjects"));
            return;
        }

        CaptureSelectedSourcesAndBeginGeometry(session, context);
    }

    private bool CaptureSelectedSourcesAndBeginGeometry(CadWorkspaceSession session, ModifyCommandContext context)
    {
        var selected = session.Interaction.Selection.SelectedEntities.ToArray();
        if (selected.Length == 0)
        {
            SetSessionStatus(session, ShellString("ModifySelectObjects"));
            return false;
        }

        context.SourceEntities = selected;
        session.CommandBasePoint = null;
        context.Phase = context.CommandName == "MIRROR" ? ModifyPhase.MirrorFirstPoint : ModifyPhase.BasePoint;
        session.Viewport.BeginModifyPointInput();
        SetSessionStatus(session, context.CommandName == "MIRROR"
            ? ShellString("ModifyMirrorFirstPoint")
            : ShellString("ModifyBasePoint"));
        return true;
    }

    private void ModifyAwareCommandInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var session = ActiveSession;
        if (session?.CommandSession.ActiveCommand?.Category != CadCommandCategory.Modify ||
            !_modifyContexts.ContainsKey(session))
        {
            CommandInput_KeyDown(sender, e);
            return;
        }

        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            var input = CommandInput.Text.Trim();
            CommandInput.Text = string.Empty;
            SubmitModifyCommandLine(session, input);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            CancelActiveCommand();
            e.Handled = true;
        }
    }

    private void SubmitModifyCommandLine(CadWorkspaceSession session, string input)
    {
        if (!_modifyContexts.TryGetValue(session, out var context))
        {
            return;
        }

        switch (context.Phase)
        {
            case ModifyPhase.SelectObjects:
                if (!string.IsNullOrWhiteSpace(input))
                {
                    SetSessionStatus(session, ShellString("ModifySelectObjects"));
                    return;
                }
                CaptureSelectedSourcesAndBeginGeometry(session, context);
                return;

            case ModifyPhase.BasePoint:
            case ModifyPhase.MirrorFirstPoint:
            case ModifyPhase.TargetPoint:
            case ModifyPhase.MirrorSecondPoint:
            case ModifyPhase.OffsetSidePoint:
                if (string.IsNullOrWhiteSpace(input) || !TryResolvePointInput(session, input, out var point))
                {
                    SetSessionStatus(session, ShellString("ModifyPointRequired"));
                    return;
                }
                OnModifyPointAccepted(session, point);
                return;

            case ModifyPhase.RotationAngle:
                if (CommandInputParser.TryParseNumber(input, out var degrees) && double.IsFinite(degrees))
                {
                    CommitRotation(session, context, degrees * Math.PI / 180.0);
                    return;
                }
                if (TryResolvePointInput(session, input, out var rotationPoint) && context.BasePoint is CadPoint rotationBase)
                {
                    CommitRotation(session, context, AngleFrom(rotationBase, rotationPoint));
                    return;
                }
                SetSessionStatus(session, ShellString("ModifyRotationAngle"));
                return;

            case ModifyPhase.ScaleFactor:
                if (CommandInputParser.TryParseNumber(input, out var factor) && double.IsFinite(factor) && factor > 1e-9)
                {
                    CommitScale(session, context, factor);
                    return;
                }
                if (TryResolvePointInput(session, input, out var scalePoint) && context.BasePoint is CadPoint scaleBase)
                {
                    factor = (scalePoint - scaleBase).Length;
                    if (factor > 1e-9)
                    {
                        CommitScale(session, context, factor);
                        return;
                    }
                }
                SetSessionStatus(session, ShellString("ModifyScaleFactor"));
                return;

            case ModifyPhase.MirrorEraseOption:
                var erase = input.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                            input.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
                            input.Equals("是", StringComparison.OrdinalIgnoreCase);
                var keep = string.IsNullOrWhiteSpace(input) ||
                           input.Equals("N", StringComparison.OrdinalIgnoreCase) ||
                           input.Equals("NO", StringComparison.OrdinalIgnoreCase) ||
                           input.Equals("否", StringComparison.OrdinalIgnoreCase);
                if (!erase && !keep)
                {
                    SetSessionStatus(session, ShellString("ModifyMirrorEraseSource"));
                    return;
                }
                CommitMirror(session, context, erase);
                return;

            case ModifyPhase.OffsetDistance:
                if (!CommandInputParser.TryParseNumber(input, out var distance) || !double.IsFinite(distance) || distance <= 1e-9)
                {
                    SetSessionStatus(session, ShellString("ModifyOffsetDistance"));
                    return;
                }
                context.OffsetDistance = distance;
                context.Phase = ModifyPhase.OffsetPickEntity;
                session.Viewport.BeginModifyEntityPickInput();
                SetSessionStatus(session, ShellString("ModifyOffsetPickEntity"));
                return;

            case ModifyPhase.TrimPick:
            case ModifyPhase.ExtendPick:
                if (string.IsNullOrWhiteSpace(input))
                {
                    CompleteModifyCommand(session, ShellString("ModifyComplete"));
                }
                return;
        }
    }

    private void OnModifyPointAccepted(CadWorkspaceSession session, CadPoint point)
    {
        if (!_modifyContexts.TryGetValue(session, out var context) ||
            session.CommandSession.ActiveCommand?.Category != CadCommandCategory.Modify)
        {
            return;
        }

        switch (context.Phase)
        {
            case ModifyPhase.BasePoint:
                context.BasePoint = point;
                session.CommandBasePoint = point;
                switch (context.CommandName)
                {
                    case "MOVE":
                    case "COPY":
                        context.Phase = ModifyPhase.TargetPoint;
                        session.Viewport.BeginModifyPointInput(
                            point,
                            useOrtho: true,
                            previewFactory: target => BuildTranslatePreview(context, target));
                        SetSessionStatus(session, context.CommandName == "MOVE"
                            ? ShellString("ModifyMoveTarget")
                            : ShellString("ModifyCopyTarget"));
                        break;
                    case "ROTATE":
                        context.Phase = ModifyPhase.RotationAngle;
                        session.Viewport.BeginModifyPointInput(
                            point,
                            previewFactory: target => BuildRotatePreview(context, target));
                        SetSessionStatus(session, ShellString("ModifyRotationAngle"));
                        break;
                    case "SCALE":
                        context.Phase = ModifyPhase.ScaleFactor;
                        session.Viewport.BeginModifyPointInput(
                            point,
                            previewFactory: target => BuildScalePreview(context, target));
                        SetSessionStatus(session, ShellString("ModifyScaleFactor"));
                        break;
                }
                break;

            case ModifyPhase.TargetPoint:
                if (context.BasePoint is not CadPoint translationBase)
                {
                    return;
                }
                var displacement = point - translationBase;
                if (displacement.Length <= 1e-9)
                {
                    SetSessionStatus(session, ShellString("ModifyPointRequired"));
                    return;
                }
                if (context.CommandName == "MOVE") CommitMove(session, context, displacement);
                else CommitCopy(session, context, displacement);
                break;

            case ModifyPhase.RotationAngle:
                if (context.BasePoint is CadPoint rotationBase)
                    CommitRotation(session, context, AngleFrom(rotationBase, point));
                break;

            case ModifyPhase.ScaleFactor:
                if (context.BasePoint is CadPoint scaleBase)
                {
                    var factor = (point - scaleBase).Length;
                    if (factor > 1e-9) CommitScale(session, context, factor);
                    else SetSessionStatus(session, ShellString("ModifyScaleFactor"));
                }
                break;

            case ModifyPhase.MirrorFirstPoint:
                context.MirrorFirstPoint = point;
                session.CommandBasePoint = point;
                context.Phase = ModifyPhase.MirrorSecondPoint;
                session.Viewport.BeginModifyPointInput(
                    point,
                    previewFactory: target => BuildMirrorPreview(context, target));
                SetSessionStatus(session, ShellString("ModifyMirrorSecondPoint"));
                break;

            case ModifyPhase.MirrorSecondPoint:
                if (context.MirrorFirstPoint is not CadPoint first || (point - first).Length <= 1e-9)
                {
                    SetSessionStatus(session, ShellString("ModifyMirrorSecondPoint"));
                    return;
                }
                context.MirrorSecondPoint = point;
                context.Phase = ModifyPhase.MirrorEraseOption;
                session.Viewport.CancelModifyInput();
                session.CommandBasePoint = null;
                SetSessionStatus(session, ShellString("ModifyMirrorEraseSource"));
                CommandInput.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                break;

            case ModifyPhase.OffsetSidePoint:
                CommitOffset(session, context, point);
                break;
        }
    }

    private void OnModifyEntityPicked(CadWorkspaceSession session, Guid id, CadPoint pickPoint)
    {
        if (!_modifyContexts.TryGetValue(session, out var context))
        {
            return;
        }
        var target = session.Document.Entities.FirstOrDefault(entity => entity.Id == id);
        if (target is null)
        {
            return;
        }

        switch (context.Phase)
        {
            case ModifyPhase.OffsetPickEntity:
                context.OffsetSourceId = id;
                context.Phase = ModifyPhase.OffsetSidePoint;
                session.CommandBasePoint = null;
                session.Viewport.BeginModifyPointInput(
                    previewFactory: point => BuildOffsetPreview(session, context, point));
                SetSessionStatus(session, ShellString("ModifyOffsetSidePoint"));
                break;

            case ModifyPhase.TrimPick:
                var trimBoundaries = session.Document.Entities.Where(entity => entity.Id != id).ToArray();
                if (CadTrimExtend.TryTrim(target, trimBoundaries, pickPoint, out var replacements) &&
                    session.Document.Replace(id, replacements))
                {
                    SetSessionStatus(session, ShellString("ModifyTrimContinue"));
                }
                else
                {
                    SetSessionStatus(session, ShellString("ModifyTrimNoBoundary"));
                }
                session.Viewport.BeginModifyEntityPickInput();
                break;

            case ModifyPhase.ExtendPick:
                var extendBoundaries = session.Document.Entities.Where(entity => entity.Id != id).ToArray();
                if (CadTrimExtend.TryExtend(target, extendBoundaries, pickPoint, out var replacement) &&
                    replacement is not null && session.Document.Replace(id, [replacement]))
                {
                    SetSessionStatus(session, ShellString("ModifyExtendContinue"));
                }
                else
                {
                    SetSessionStatus(session, ShellString("ModifyExtendNoBoundary"));
                }
                session.Viewport.BeginModifyEntityPickInput();
                break;
        }
    }

    private void CommitMove(CadWorkspaceSession session, ModifyCommandContext context, CadVector displacement)
    {
        var replacements = context.SourceEntities
            .Select(entity => CadEntityTransform.Translate(entity, displacement))
            .ToArray();
        if (session.Document.ReplaceRange(replacements) > 0)
            CompleteModifyCommand(session, ShellString("ModifyMoveComplete"));
    }

    private void CommitCopy(CadWorkspaceSession session, ModifyCommandContext context, CadVector displacement)
    {
        var copies = context.SourceEntities
            .Select(entity => CadEntityTransform.Translate(entity, displacement, preserveIdentity: false))
            .ToArray();
        session.Document.AddRange(copies);
        CompleteModifyCommand(session, ShellString("ModifyCopyComplete"));
    }

    private void CommitRotation(CadWorkspaceSession session, ModifyCommandContext context, double angleRadians)
    {
        if (context.BasePoint is not CadPoint basePoint || !double.IsFinite(angleRadians))
        {
            return;
        }
        var replacements = context.SourceEntities
            .Select(entity => CadEntityTransform.Rotate(entity, basePoint, angleRadians))
            .ToArray();
        if (session.Document.ReplaceRange(replacements) > 0)
            CompleteModifyCommand(session, ShellString("ModifyRotateComplete"));
    }

    private void CommitScale(CadWorkspaceSession session, ModifyCommandContext context, double factor)
    {
        if (context.BasePoint is not CadPoint basePoint || factor <= 1e-9)
        {
            return;
        }
        var replacements = context.SourceEntities
            .Select(entity => CadEntityTransform.Scale(entity, basePoint, factor))
            .ToArray();
        if (session.Document.ReplaceRange(replacements) > 0)
            CompleteModifyCommand(session, ShellString("ModifyScaleComplete"));
    }

    private void CommitMirror(CadWorkspaceSession session, ModifyCommandContext context, bool eraseSource)
    {
        if (context.MirrorFirstPoint is not CadPoint first || context.MirrorSecondPoint is not CadPoint second)
        {
            return;
        }

        if (eraseSource)
        {
            var replacements = context.SourceEntities
                .Select(entity => CadEntityTransform.Mirror(entity, first, second))
                .ToArray();
            session.Document.ReplaceRange(replacements);
        }
        else
        {
            var copies = context.SourceEntities
                .Select(entity => CadEntityTransform.Mirror(entity, first, second, preserveIdentity: false))
                .ToArray();
            session.Document.AddRange(copies);
        }
        CompleteModifyCommand(session, ShellString("ModifyMirrorComplete"));
    }

    private void CommitOffset(CadWorkspaceSession session, ModifyCommandContext context, CadPoint sidePoint)
    {
        var source = session.Document.Entities.FirstOrDefault(entity => entity.Id == context.OffsetSourceId);
        if (source is null || context.OffsetDistance <= 1e-9 ||
            !CadOffset.TryCreate(source, context.OffsetDistance, sidePoint, out var offset) || offset is null)
        {
            SetSessionStatus(session, ShellString("ModifyOffsetInvalid"));
            return;
        }

        session.Document.Add(offset);
        CompleteModifyCommand(session, ShellString("ModifyOffsetComplete"));
    }

    private void CompleteModifyCommand(CadWorkspaceSession session, string status)
    {
        session.Viewport.CancelModifyInput();
        session.CommandBasePoint = null;
        session.CommandSession.Complete();
        SetSessionStatus(session, status);
        UpdateSessionUi(session);
    }

    private static IReadOnlyList<ICadEntity> BuildTranslatePreview(ModifyCommandContext context, CadPoint target)
    {
        if (context.BasePoint is not CadPoint basePoint)
        {
            return [];
        }
        var displacement = target - basePoint;
        return context.SourceEntities.Select(entity => CadEntityTransform.Translate(entity, displacement, false)).ToArray();
    }

    private static IReadOnlyList<ICadEntity> BuildRotatePreview(ModifyCommandContext context, CadPoint target)
    {
        if (context.BasePoint is not CadPoint basePoint || (target - basePoint).Length <= 1e-9)
        {
            return [];
        }
        var angle = AngleFrom(basePoint, target);
        return context.SourceEntities.Select(entity => CadEntityTransform.Rotate(entity, basePoint, angle, false)).ToArray();
    }

    private static IReadOnlyList<ICadEntity> BuildScalePreview(ModifyCommandContext context, CadPoint target)
    {
        if (context.BasePoint is not CadPoint basePoint)
        {
            return [];
        }
        var factor = (target - basePoint).Length;
        if (factor <= 1e-9)
        {
            return [];
        }
        return context.SourceEntities.Select(entity => CadEntityTransform.Scale(entity, basePoint, factor, false)).ToArray();
    }

    private static IReadOnlyList<ICadEntity> BuildMirrorPreview(ModifyCommandContext context, CadPoint target)
    {
        if (context.MirrorFirstPoint is not CadPoint first || (target - first).Length <= 1e-9)
        {
            return [];
        }
        return context.SourceEntities.Select(entity => CadEntityTransform.Mirror(entity, first, target, false)).ToArray();
    }

    private static IReadOnlyList<ICadEntity> BuildOffsetPreview(
        CadWorkspaceSession session,
        ModifyCommandContext context,
        CadPoint sidePoint)
    {
        var source = session.Document.Entities.FirstOrDefault(entity => entity.Id == context.OffsetSourceId);
        if (source is null || !CadOffset.TryCreate(source, context.OffsetDistance, sidePoint, out var offset) || offset is null)
        {
            return [];
        }
        return [offset];
    }

    private static double AngleFrom(CadPoint basePoint, CadPoint point) =>
        Math.Atan2(point.Y - basePoint.Y, point.X - basePoint.X);

    private sealed class ModifyCommandContext(string commandName)
    {
        public string CommandName { get; } = commandName;
        public ModifyPhase Phase { get; set; }
        public ICadEntity[] SourceEntities { get; set; } = [];
        public CadPoint? BasePoint { get; set; }
        public CadPoint? MirrorFirstPoint { get; set; }
        public CadPoint? MirrorSecondPoint { get; set; }
        public double OffsetDistance { get; set; }
        public Guid OffsetSourceId { get; set; }
    }

    private enum ModifyPhase
    {
        SelectObjects,
        BasePoint,
        TargetPoint,
        RotationAngle,
        ScaleFactor,
        MirrorFirstPoint,
        MirrorSecondPoint,
        MirrorEraseOption,
        OffsetDistance,
        OffsetPickEntity,
        OffsetSidePoint,
        TrimPick,
        ExtendPick
    }
}
