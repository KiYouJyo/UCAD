using ACadSharp.Entities;
using ACadSharp.Objects;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using AcadDocument = ACadSharp.CadDocument;
using AcadLayout = ACadSharp.Objects.Layout;
using AcadViewport = ACadSharp.Entities.Viewport;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Maps AutoCAD paper layouts, plot settings and rectangular paper-space viewports to UCAD's
/// layout model. The adapter intentionally ignores model-space layout metadata and non-rectangular
/// viewport clipping until UCAD has a matching semantic model.
/// </summary>
internal static class CadAcadLayoutInterop
{
    private const double MinimumPaperMm = 1.0;

    public static void Import(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var layouts = new List<CadLayoutDefinition>();
        foreach (var sourceLayout in source.Layouts.Where(layout => layout.IsPaperSpace).OrderBy(layout => layout.TabOrder))
        {
            try
            {
                layouts.Add(ConvertLayout(sourceLayout, warnings));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArgumentOutOfRangeException)
            {
                warnings.Add($"DWG layout '{sourceLayout.Name}' could not be imported: {ex.Message}");
            }
        }

        if (layouts.Count == 0) return;
        var activeName = layouts.First().Name;
        target.SetLayoutTable(layouts, activeName);
    }

    public static void Export(UcadDocument source, AcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var ordered = source.Layouts.Where(layout => !layout.ModelLayout).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var sourceLayout = ordered[index];
            AcadLayout targetLayout;
            if (target.Layouts.TryGet(sourceLayout.Name, out var existing))
            {
                targetLayout = existing;
            }
            else
            {
                targetLayout = new AcadLayout(sourceLayout.Name, GetPaperBlockName(target, sourceLayout.Name));
                target.Layouts.Add(targetLayout);
            }

            targetLayout.TabOrder = index + 1;
            ApplyPageSetup(sourceLayout.PageSetup, targetLayout);
            targetLayout.UpdatePaperViewport();
            ReplaceViewports(sourceLayout, targetLayout, warnings);
        }

        var sourceNames = ordered.Select(layout => layout.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var extra in target.Layouts
                     .Where(layout => layout.IsPaperSpace)
                     .Where(layout => !sourceNames.Contains(layout.Name))
                     .Where(layout => !string.Equals(layout.Name, AcadLayout.PaperLayoutName, StringComparison.OrdinalIgnoreCase))
                     .Select(layout => layout.Name)
                     .ToArray())
        {
            target.Layouts.Remove(extra);
        }

        if (!sourceNames.Contains(AcadLayout.PaperLayoutName) && target.Layouts.TryGet(AcadLayout.PaperLayoutName, out var unavoidableDefault))
        {
            warnings.Add($"DWG export retained AutoCAD's mandatory default paper layout '{unavoidableDefault.Name}' because ACadSharp does not allow removing it.");
        }
    }

    private static CadLayoutDefinition ConvertLayout(AcadLayout layout, List<string> warnings)
    {
        var pageSetup = ConvertPageSetup(layout, warnings);
        var paperUnitToMm = layout.PaperUnits switch
        {
            PlotPaperUnits.Inches => 25.4,
            PlotPaperUnits.Millimeters => 1.0,
            PlotPaperUnits.Pixels => 1.0,
            _ => 1.0
        };
        if (layout.PaperUnits == PlotPaperUnits.Pixels)
            warnings.Add($"DWG layout '{layout.Name}' uses raster pixel paper units; viewport paper coordinates were treated as millimetres.");

        var viewports = new List<CadLayoutViewport>();
        foreach (var viewport in layout.Viewports ?? [])
        {
            if (viewport.RepresentsPaper) continue;
            if (viewport.Boundary is not null)
            {
                warnings.Add($"DWG layout '{layout.Name}' viewport {viewport.Id} uses non-rectangular clipping; UCAD imported its rectangular viewport bounds only.");
            }
            if (viewport.Width <= 0 || viewport.Height <= 0 || viewport.ViewHeight <= 0) continue;

            var widthMm = viewport.Width * paperUnitToMm;
            var heightMm = viewport.Height * paperUnitToMm;
            var centerX = viewport.Center.X * paperUnitToMm;
            var centerY = viewport.Center.Y * paperUnitToMm;
            var paperRect = new CadRect(
                centerX - widthMm / 2,
                centerY - heightMm / 2,
                centerX + widthMm / 2,
                centerY + heightMm / 2);
            var denominator = viewport.ViewHeight / heightMm;
            if (!double.IsFinite(denominator) || denominator <= 0) denominator = 1;

            viewports.Add(new CadLayoutViewport(
                $"Viewport {viewport.Id}",
                paperRect,
                new CadPoint(viewport.ViewTarget.X, viewport.ViewTarget.Y),
                denominator,
                viewport.TwistAngle,
                viewport.Status.HasFlag(ViewportStatusFlags.ViewportZoomLocking)));
        }

        return new CadLayoutDefinition(layout.Name, pageSetup, viewports);
    }

    private static CadPageSetup ConvertPageSetup(PlotSettings source, List<string> warnings)
    {
        var width = Math.Max(MinimumPaperMm, source.PaperWidth);
        var height = Math.Max(MinimumPaperMm, source.PaperHeight);
        var landscape = width >= height;
        var paperName = string.IsNullOrWhiteSpace(source.PaperSize) ? "AutoCAD paper" : source.PaperSize;
        var paper = new CadPaperSize(paperName, Math.Min(width, height), Math.Max(width, height));
        var margin = source.UnprintableMargin;
        var left = SafeMargin(margin.Left);
        var top = SafeMargin(margin.Top);
        var right = SafeMargin(margin.Right);
        var bottom = SafeMargin(margin.Bottom);
        ClampMargins(width, height, ref left, ref top, ref right, ref bottom, warnings, source.Name);

        var plotArea = source.PlotType switch
        {
            PlotType.LastScreenDisplay => CadPlotArea.Display,
            PlotType.DrawingExtents => CadPlotArea.Extents,
            PlotType.Window => CadPlotArea.Window,
            PlotType.LayoutInformation => CadPlotArea.Layout,
            PlotType.DrawingLimits => CadPlotArea.Extents,
            PlotType.View => CadPlotArea.Display,
            _ => CadPlotArea.Layout
        };
        if (source.PlotType is PlotType.DrawingLimits or PlotType.View)
            warnings.Add($"AutoCAD plot type '{source.PlotType}' has no exact UCAD page-setup mode and was mapped to {plotArea}.");

        CadRect? window = null;
        if (plotArea == CadPlotArea.Window)
        {
            var first = new CadPoint(source.WindowLowerLeftX, source.WindowLowerLeftY);
            var second = new CadPoint(source.WindowUpperLeftX, source.WindowUpperLeftY);
            var rect = CadRect.FromPoints(first, second);
            if (rect.Width > 0 && rect.Height > 0) window = rect;
            else
            {
                plotArea = CadPlotArea.Extents;
                warnings.Add($"AutoCAD layout '{source.Name}' has an invalid plot window and was mapped to Extents.");
            }
        }

        var denominator = ResolvePlotScaleDenominator(source, warnings);
        var style = ResolvePlotStyle(source.StyleSheet);
        return new CadPageSetup(
            paper,
            landscape,
            left,
            top,
            right,
            bottom,
            denominator,
            plotArea,
            style,
            window);
    }

    private static void ApplyPageSetup(CadPageSetup source, AcadLayout target)
    {
        target.PaperUnits = PlotPaperUnits.Millimeters;
        target.PaperWidth = source.PaperWidthMm;
        target.PaperHeight = source.PaperHeightMm;
        target.PaperSize = source.PaperSize.Name;
        target.PageName = source.PaperSize.Name;
        target.PaperRotation = PlotRotation.NoRotation;
        target.UnprintableMargin = new PaperMargin(
            source.MarginLeftMm,
            source.MarginBottomMm,
            source.MarginRightMm,
            source.MarginTopMm);
        target.NumeratorScale = 1;
        target.DenominatorScale = source.PlotScaleDenominator;
        target.StandardScale = 1.0 / source.PlotScaleDenominator;
        target.PlotType = source.PlotArea switch
        {
            CadPlotArea.Display => PlotType.LastScreenDisplay,
            CadPlotArea.Extents => PlotType.DrawingExtents,
            CadPlotArea.Window => PlotType.Window,
            CadPlotArea.Layout => PlotType.LayoutInformation,
            _ => PlotType.LayoutInformation
        };
        target.StyleSheet = source.PlotStyle switch
        {
            CadPlotStyleMode.Monochrome => "monochrome.ctb",
            CadPlotStyleMode.Grayscale => "grayscale.ctb",
            _ => string.Empty
        };
        if (source.ModelWindow is CadRect window)
        {
            target.WindowLowerLeftX = window.Left;
            target.WindowLowerLeftY = window.Bottom;
            target.WindowUpperLeftX = window.Right;
            target.WindowUpperLeftY = window.Top;
        }
    }

    private static void ReplaceViewports(CadLayoutDefinition source, AcadLayout target, List<string> warnings)
    {
        var paperViewport = target.PaperViewport;
        foreach (var viewport in target.Viewports.Where(viewport => !ReferenceEquals(viewport, paperViewport)).ToArray())
            target.AssociatedBlock.Entities.Remove(viewport);

        foreach (var sourceViewport in source.Viewports)
        {
            var center = sourceViewport.PaperCenterMm;
            var viewport = new AcadViewport
            {
                Center = new CSMath.XYZ(center.X, center.Y, 0),
                Width = sourceViewport.PaperRectMm.Width,
                Height = sourceViewport.PaperRectMm.Height,
                ViewTarget = new CSMath.XYZ(sourceViewport.ModelCenter.X, sourceViewport.ModelCenter.Y, 0),
                ViewCenter = CSMath.XY.Zero,
                ViewDirection = CSMath.XYZ.AxisZ,
                ViewHeight = sourceViewport.PaperRectMm.Height * sourceViewport.ScaleDenominator,
                TwistAngle = sourceViewport.TwistAngleRadians,
                ActiveStatus = 1,
                Status = sourceViewport.Locked
                    ? ViewportStatusFlags.ViewportZoomLocking | ViewportStatusFlags.CurrentlyAlwaysEnabled
                    : ViewportStatusFlags.CurrentlyAlwaysEnabled
            };
            target.AddViewport(viewport);
        }

        if (target.PaperViewport is null)
        {
            warnings.Add($"DWG export could not establish the paper viewport for layout '{source.Name}'.");
        }
    }

    private static double ResolvePlotScaleDenominator(PlotSettings source, List<string> warnings)
    {
        if (source.NumeratorScale <= 0 || source.DenominatorScale <= 0) return 1;
        var paperFactor = source.PaperUnits switch
        {
            PlotPaperUnits.Inches => 25.4,
            PlotPaperUnits.Millimeters => 1.0,
            PlotPaperUnits.Pixels => 1.0,
            _ => 1.0
        };
        if (source.PaperUnits == PlotPaperUnits.Pixels)
            warnings.Add($"AutoCAD plot scale for '{source.Name}' uses pixels; UCAD retained the numeric scale ratio without DPI conversion.");
        var denominator = source.DenominatorScale / (source.NumeratorScale * paperFactor);
        return double.IsFinite(denominator) && denominator > 0 ? denominator : 1;
    }

    private static CadPlotStyleMode ResolvePlotStyle(string? styleSheet)
    {
        if (styleSheet?.Contains("monochrome", StringComparison.OrdinalIgnoreCase) == true) return CadPlotStyleMode.Monochrome;
        if (styleSheet?.Contains("gray", StringComparison.OrdinalIgnoreCase) == true) return CadPlotStyleMode.Grayscale;
        return CadPlotStyleMode.Color;
    }

    private static double SafeMargin(double value) => double.IsFinite(value) && value >= 0 ? value : 0;

    private static void ClampMargins(
        double width,
        double height,
        ref double left,
        ref double top,
        ref double right,
        ref double bottom,
        List<string> warnings,
        string layoutName)
    {
        if (left + right < width && top + bottom < height) return;
        left = right = Math.Min(10, width * 0.05);
        top = bottom = Math.Min(10, height * 0.05);
        warnings.Add($"AutoCAD layout '{layoutName}' contains invalid/unprintable margins; UCAD used safe 5% margins.");
    }

    private static string GetPaperBlockName(AcadDocument document, string layoutName)
    {
        var ordinal = document.Layouts.Count(layout => layout.IsPaperSpace);
        return ordinal == 0 ? "*Paper_Space" : $"*Paper_Space{ordinal}";
    }
}
