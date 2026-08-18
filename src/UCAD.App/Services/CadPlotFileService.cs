using UCAD.Core;
using UCAD.Core.Layout;
using UCAD.Core.Plot;

namespace UCAD.Services;

public sealed class CadPlotFileService
{
    public async Task<CadPdfExportResult> ExportPdfAsync(
        string filePath,
        CadDocument document,
        CadPlotPlan plan,
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(plan);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var result = CadPdfExporter.Export(document, plan, title);
        var tempPath = fullPath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, result.Content, cancellationToken);
        File.Move(tempPath, fullPath, overwrite: true);
        return result;
    }

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