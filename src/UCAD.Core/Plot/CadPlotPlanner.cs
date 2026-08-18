using UCAD.Core.Layout;

namespace UCAD.Core.Plot;

public static class CadPlotPlanner
{
    /// <summary>
    /// Resolves the plans that compose one physical output page. Paper-space viewports
    /// participate only when PlotArea is Layout. Extents/Display/Window retain the
    /// caller-provided fallback plan even if the active layout also owns viewports.
    /// </summary>
    public static IReadOnlyList<CadPlotPlan> ResolvePagePlans(CadDocument document, CadPlotPlan fallbackPlan)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fallbackPlan);

        var layout = document.ActiveLayout;
        if (layout.PageSetup.PlotArea != CadPlotArea.Layout || layout.Viewports.Count == 0)
            return [fallbackPlan];

        return layout.Viewports
            .Select(viewport => CadPlotPlan.FromViewport(layout.PageSetup, viewport))
            .ToArray();
    }
}
