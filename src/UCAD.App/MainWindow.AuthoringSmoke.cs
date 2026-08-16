using Microsoft.UI.Xaml;
using UCAD.Core.Blocks;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Core.Modify;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    internal void ScheduleAuthoringSmoke()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("UCAD_AUTHORING_SMOKE"), "1", StringComparison.Ordinal)) return;
        RootLayout.Loaded += RootLayout_AuthoringSmokeLoaded;
    }

    private void RootLayout_AuthoringSmokeLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= RootLayout_AuthoringSmokeLoaded;
        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                CreateNewWorkspace();
                var session = ActiveSession ?? throw new InvalidOperationException("Authoring smoke could not create a Drawing workspace.");
                EnsureAuthoringSessionSubscribed(session);
                ValidateAuthoringSmoke(session);
                App.WriteStartupEvent("Authoring smoke: LAYERS + PROPERTIES + TEXT + DIM + HATCH + BLOCK + INSERT + EXPLODE initialized");
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure("AuthoringSmoke", ex);
                throw;
            }
        });
    }

    private void ValidateAuthoringSmoke(CadWorkspaceSession session)
    {
        foreach (var token in new[] { "LAYER", "CHPROP", "TEXT", "DIM", "HATCH", "BLOCK", "INSERT", "EXPLODE" })
        {
            if (!_commandRegistry.TryResolve(token, out var command) || command is null)
                throw new InvalidOperationException($"Authoring smoke command registration failed: {token}.");
        }
        if (!AnnotateCategoryButton.IsEnabled || !LayersCategoryButton.IsEnabled || !BlocksCategoryButton.IsEnabled)
            throw new InvalidOperationException("Authoring smoke capability categories are not enabled.");

        session.Document.CreateLayer(new CadLayer("Smoke", "#40A0FF", 0.50));
        session.Document.SetCurrentLayer("Smoke");
        var boundary = new PolylineEntity([
            new CadPoint(0, 0), new CadPoint(20, 0), new CadPoint(20, 10), new CadPoint(0, 10)
        ], closed: true);
        session.Document.Add(boundary);
        if (session.Document.GetEntityProperties(boundary.Id).LayerName != "Smoke")
            throw new InvalidOperationException("Authoring smoke current-layer inheritance failed.");
        session.Document.SetEntityProperties([boundary.Id], properties => properties with { ColorHex = "#FFD040", LineWeight = 0.70 });
        CadEntityProperties boundaryProperties = session.Document.GetEntityProperties(boundary.Id);
        if (boundaryProperties.ColorHex != "#FFD040" || Math.Abs(boundaryProperties.LineWeight!.Value - 0.70) > 1e-9)
            throw new InvalidOperationException("Authoring smoke entity property edit failed.");

        session.Document.UpdateLayer("Smoke", isLocked: true);
        session.Interaction.Selection.Replace(boundary.Id);
        if (!session.Interaction.Selection.IsEmpty)
            throw new InvalidOperationException("Authoring smoke locked-layer selection guard failed.");
        session.Document.UpdateLayer("Smoke", isLocked: false);
        session.Interaction.Selection.Replace(boundary.Id);
        if (session.Interaction.Selection.Count != 1)
            throw new InvalidOperationException("Authoring smoke selectable layer restore failed.");

        var text = new TextEntity(new CadPoint(2, 12), "UCAD", 3);
        var dimension = new LinearDimensionEntity(new CadPoint(0, 0), new CadPoint(20, 0), new CadPoint(0, -4));
        var hatch = new HatchEntity(boundary.Points);
        session.Document.Add(text);
        session.Document.Add(dimension);
        session.Document.Add(hatch, boundaryProperties);

        var definition = new CadBlockDefinition("SmokeBlock", new CadPoint(0, 0), [boundary, text]);
        session.Document.DefineBlock(definition);
        var reference = CadBlockFactory.CreateReference(definition, new CadPoint(40, 20), 1.5, Math.PI / 6);
        session.Document.Add(reference);
        var movedReference = AssertBlockReference(CadEntityTransform.Translate(reference, new CadVector(5, 0)));
        if (movedReference.Id != reference.Id || movedReference.Contents.Count != 2)
            throw new InvalidOperationException("Authoring smoke block transform failed.");

        var exploded = CadBlockFactory.Explode(reference);
        if (exploded.Count != 2 || exploded.Any(entity => reference.Contents.Any(child => child.Id == entity.Id)))
            throw new InvalidOperationException("Authoring smoke block explode identity failed.");
        if (!session.Document.Replace(reference.Id, exploded))
            throw new InvalidOperationException("Authoring smoke EXPLODE document mutation failed.");
        if (!session.Document.Undo() || session.Document.Entities.All(entity => entity.Id != reference.Id))
            throw new InvalidOperationException("Authoring smoke EXPLODE Undo failed.");

        session.Viewport.InvalidateInteraction();
    }

    private static BlockReferenceEntity AssertBlockReference(UCAD.Core.Entities.ICadEntity entity) =>
        entity as BlockReferenceEntity ?? throw new InvalidOperationException("Authoring smoke expected a block reference.");
}
