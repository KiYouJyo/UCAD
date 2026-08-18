using UCAD.Core;
using UCAD.Core.Layout;
using UCAD.Core.Plot;

namespace UCAD.Services;

public sealed class CadPlotFileService
{
    public async Task<CadPdfExportResult> ExportPdfAsync(
        string filePath,
        CadDocument document,
        CadPlotPlan fallbackPlan,
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fallbackPlan);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var plans = CadPlotPlanner.ResolvePagePlans(document, fallbackPlan);
        var result = CadPdfExporter.Export(document, plans, title);
        var tempPath = fullPath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, result.Content, cancellationToken);
            File.Move(tempPath, fullPath, overwrite: true);
            return result;
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public IReadOnlyList<CadPlotPlan> CreateOutputPlans(CadDocument document, CadPlotPlan fallbackPlan) =>
        CadPlotPlanner.ResolvePagePlans(document, fallbackPlan);

    public CadPlotPlan CreatePlan(CadDocument document, CadPageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pageSetup);

        if (pageSetup.PlotArea == CadPlotArea.Window && pageSetup.ModelWindow is CadRect modelWindow)
            return CadPlotPlan.FitExtents(pageSetup, modelWindow);

        if (!CadPlotGeometry.TryGetDocumentExtents(document, out var extents))
            throw new InvalidOperationException("The drawing has no finite printable extents.");

        return pageSetup.PlotArea switch
        {
            CadPlotArea.Extents or CadPlotArea.Display or CadPlotArea.Layout => CadPlotPlan.FitExtents(pageSetup, extents),
            _ => CadPlotPlan.FitExtents(pageSetup, extents)
        };
    }
}
