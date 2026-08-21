using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private readonly Dictionary<Guid, PdfUnderlayPageResource> _pdfUnderlayPages = [];
    private readonly HashSet<Guid> _underlayLoads = [];
    private readonly HashSet<Guid> _underlayLoadFailures = [];
    private bool _underlayUnloadHookInstalled;

    private void DrawUnderlayReferenceEntity(CanvasDrawingSession ds, UnderlayReferenceEntity underlay, Color color, float strokeWidth)
    {
        if (underlay.Kind == CadUnderlayKind.Pdf &&
            underlay.IsResolved &&
            _pdfUnderlayPages.TryGetValue(underlay.Id, out var resource))
        {
            DrawResolvedPdfUnderlay(ds, underlay, resource, color.A / 255f);
            return;
        }

        if (underlay.Kind == CadUnderlayKind.Pdf && underlay.IsResolved)
            QueuePdfUnderlayLoad(underlay);

        DrawUnderlayReferenceFrame(ds, underlay, color, strokeWidth);
    }

    private void QueuePdfUnderlayLoad(UnderlayReferenceEntity underlay)
    {
        if (string.IsNullOrWhiteSpace(underlay.ResolvedPath) ||
            _pdfUnderlayPages.ContainsKey(underlay.Id) ||
            _underlayLoadFailures.Contains(underlay.Id) ||
            !_underlayLoads.Add(underlay.Id)) return;

        if (!_underlayUnloadHookInstalled)
        {
            _underlayUnloadHookInstalled = true;
            Canvas.Unloaded += (_, _) => DisposeUnderlayResources();
        }

        _ = LoadPdfUnderlayAsync(underlay);
    }

    private async Task LoadPdfUnderlayAsync(UnderlayReferenceEntity underlay)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(underlay.ResolvedPath)) return;
            var file = await StorageFile.GetFileFromPathAsync(underlay.ResolvedPath);
            var document = await PdfDocument.LoadFromFileAsync(file);
            if (document.PageCount == 0) throw new InvalidDataException("The referenced PDF contains no pages.");

            var pageIndex = ResolvePdfPageIndex(underlay.Page, document.PageCount);
            using var page = document.GetPage(pageIndex);
            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream);
            stream.Seek(0);
            var bitmap = await CanvasBitmap.LoadAsync(Canvas, stream);
            var pageSize = page.Size;

            if (_pdfUnderlayPages.Remove(underlay.Id, out var previous)) previous.Dispose();
            _pdfUnderlayPages[underlay.Id] = new PdfUnderlayPageResource(bitmap, pageSize);
            _underlayLoadFailures.Remove(underlay.Id);
            Canvas.Invalidate();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or COMException)
        {
            // Keep the exact insertion/clip reference visible. A corrupt/unsupported external PDF
            // must never abort the CAD drawing render loop or repeatedly retry every frame.
            _underlayLoadFailures.Add(underlay.Id);
        }
        finally
        {
            _underlayLoads.Remove(underlay.Id);
        }
    }

    private static uint ResolvePdfPageIndex(string? pageToken, uint pageCount)
    {
        if (!string.IsNullOrWhiteSpace(pageToken) &&
            uint.TryParse(pageToken.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var oneBased) &&
            oneBased >= 1 && oneBased <= pageCount)
            return oneBased - 1;

        return 0;
    }

    private void DrawResolvedPdfUnderlay(
        CanvasDrawingSession ds,
        UnderlayReferenceEntity underlay,
        PdfUnderlayPageResource resource,
        float entityOpacity)
    {
        var widthInches = Math.Max(resource.PageSize.Width / 96.0, 1e-9);
        var heightInches = Math.Max(resource.PageSize.Height / 96.0, 1e-9);
        var pageCorners = GetUnderlayPageWorldCorners(underlay, widthInches, heightInches);

        CanvasGeometry? clipGeometry = null;
        CanvasActiveLayer? clipLayer = null;
        try
        {
            if (underlay.ClipBoundary.Count >= 3)
            {
                clipGeometry = underlay.ClipInside
                    ? CreateWorldPolygonGeometry(ds, underlay.ClipBoundary)
                    : CreateInverseUnderlayClipGeometry(ds, pageCorners, underlay.ClipBoundary);
                clipLayer = ds.CreateLayer(1f, clipGeometry);
            }

            // Windows.Data.Pdf rasterizes with a top-left pixel origin while AutoCAD places
            // PDF underlays from the lower-left corner. Anchor at the page's world upper-left
            // and reverse the local V basis so the page is neither upside-down nor mirrored.
            var lowerLeft = underlay.InsertionPoint;
            var radians = underlay.RotationRadians;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var uWorld = new CadVector(cos * underlay.XScale, sin * underlay.XScale);
            var vWorld = new CadVector(-sin * underlay.YScale, cos * underlay.YScale);
            var upperLeft = lowerLeft + (vWorld * heightInches);
            var origin = WorldToScreen(upperLeft);
            var uEnd = WorldToScreen(upperLeft + uWorld);
            var vEnd = WorldToScreen(upperLeft - vWorld);
            var u = uEnd - origin;
            var v = vEnd - origin;
            var transform = new Matrix3x2(u.X, u.Y, v.X, v.Y, origin.X, origin.Y);

            var previous = ds.Transform;
            try
            {
                ds.Transform = transform;
                var destination = new Rect(0, 0, widthInches, heightInches);
                var opacity = Math.Clamp((1f - (underlay.Fade / 100f)) * entityOpacity, 0f, 1f);
                ds.DrawImage(resource.Bitmap, destination, resource.Bitmap.Bounds, opacity);
            }
            finally
            {
                ds.Transform = previous;
            }
        }
        finally
        {
            clipLayer?.Dispose();
            clipGeometry?.Dispose();
        }
    }

    private IReadOnlyList<CadPoint> GetUnderlayPageWorldCorners(UnderlayReferenceEntity underlay, double width, double height)
    {
        var radians = underlay.RotationRadians;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var u = new CadVector(cos * underlay.XScale, sin * underlay.XScale) * width;
        var v = new CadVector(-sin * underlay.YScale, cos * underlay.YScale) * height;
        var lowerLeft = underlay.InsertionPoint;
        return [lowerLeft, lowerLeft + u, lowerLeft + u + v, lowerLeft + v];
    }

    private CanvasGeometry CreateWorldPolygonGeometry(CanvasDrawingSession ds, IReadOnlyList<CadPoint> boundary)
    {
        using var builder = new CanvasPathBuilder(ds.Device);
        builder.BeginFigure(WorldToScreen(boundary[0]));
        for (var i = 1; i < boundary.Count; i++) builder.AddLine(WorldToScreen(boundary[i]));
        builder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(builder);
    }

    private CanvasGeometry CreateInverseUnderlayClipGeometry(
        CanvasDrawingSession ds,
        IReadOnlyList<CadPoint> pageBoundary,
        IReadOnlyList<CadPoint> excludedBoundary)
    {
        using var builder = new CanvasPathBuilder(ds.Device);
        builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Alternate);
        AddUnderlayClipLoop(builder, pageBoundary);
        AddUnderlayClipLoop(builder, excludedBoundary);
        return CanvasGeometry.CreatePath(builder);
    }

    private void AddUnderlayClipLoop(CanvasPathBuilder builder, IReadOnlyList<CadPoint> boundary)
    {
        if (boundary.Count < 3) return;
        builder.BeginFigure(WorldToScreen(boundary[0]));
        for (var i = 1; i < boundary.Count; i++) builder.AddLine(WorldToScreen(boundary[i]));
        builder.EndFigure(CanvasFigureLoop.Closed);
    }

    private void DrawUnderlayReferenceFrame(CanvasDrawingSession ds, UnderlayReferenceEntity underlay, Color color, float strokeWidth)
    {
        if (underlay.ClipBoundary.Count >= 2)
        {
            var points = underlay.ClipBoundary.Select(WorldToScreen).ToArray();
            for (var i = 1; i < points.Length; i++) ds.DrawLine(points[i - 1], points[i], color, strokeWidth);
            ds.DrawLine(points[^1], points[0], color, strokeWidth);
            DrawUnderlayLabel(ds, underlay, points[0], color);
            return;
        }

        var anchor = WorldToScreen(underlay.InsertionPoint);
        const float mark = 5f;
        ds.DrawLine(anchor.X - mark, anchor.Y, anchor.X + mark, anchor.Y, color, strokeWidth);
        ds.DrawLine(anchor.X, anchor.Y - mark, anchor.X, anchor.Y + mark, color, strokeWidth);
        DrawUnderlayLabel(ds, underlay, anchor, color);
    }

    private static void DrawUnderlayLabel(CanvasDrawingSession ds, UnderlayReferenceEntity underlay, Vector2 anchor, Color color)
    {
        using var format = new CanvasTextFormat { FontSize = 10, WordWrapping = CanvasWordWrapping.NoWrap };
        var page = string.IsNullOrWhiteSpace(underlay.Page) ? string.Empty : $" [{underlay.Page}]";
        var state = underlay.IsResolved ? string.Empty : ": unresolved";
        ds.DrawText($"{underlay.Kind.ToString().ToUpperInvariant()}{page}{state}", anchor + new Vector2(4, -14), color, format);
    }

    private void DisposeUnderlayResources()
    {
        foreach (var resource in _pdfUnderlayPages.Values) resource.Dispose();
        _pdfUnderlayPages.Clear();
        _underlayLoads.Clear();
        _underlayLoadFailures.Clear();
    }

    private sealed class PdfUnderlayPageResource(CanvasBitmap bitmap, Size pageSize) : IDisposable
    {
        public CanvasBitmap Bitmap { get; } = bitmap;
        public Size PageSize { get; } = pageSize;
        public void Dispose() => Bitmap.Dispose();
    }
}
