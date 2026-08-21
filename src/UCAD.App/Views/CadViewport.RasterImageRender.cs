using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System.Numerics;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Windows.Foundation;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private readonly Dictionary<Guid, CanvasBitmap> _rasterImageBitmaps = [];
    private readonly HashSet<Guid> _rasterImageLoads = [];
    private bool _rasterImageUnloadHookInstalled;

    private void DrawRasterImageEntity(CanvasDrawingSession ds, RasterImageEntity image, Color color, float strokeWidth)
    {
        if (image.IsResolved && _rasterImageBitmaps.TryGetValue(image.Id, out var bitmap))
        {
            DrawResolvedRasterImage(ds, image, bitmap);
            return;
        }

        if (image.IsResolved) QueueRasterImageLoad(image);
        DrawRasterReferenceFrame(ds, image, color, strokeWidth);
    }

    private void QueueRasterImageLoad(RasterImageEntity image)
    {
        if (string.IsNullOrWhiteSpace(image.ResolvedPath) ||
            _rasterImageBitmaps.ContainsKey(image.Id) ||
            !_rasterImageLoads.Add(image.Id)) return;

        if (!_rasterImageUnloadHookInstalled)
        {
            _rasterImageUnloadHookInstalled = true;
            Canvas.Unloaded += (_, _) => DisposeRasterImageResources();
        }
        _ = LoadRasterImageAsync(image);
    }

    private async Task LoadRasterImageAsync(RasterImageEntity image)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(image.ResolvedPath)) return;
            var bitmap = await CanvasBitmap.LoadAsync(Canvas, image.ResolvedPath);
            if (_rasterImageBitmaps.Remove(image.Id, out var previous)) previous.Dispose();
            _rasterImageBitmaps[image.Id] = bitmap;
            Canvas.Invalidate();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            // Keep the exact reference frame visible. The file-service resolver already reports
            // missing paths; decode/device failures must not make the rest of the CAD drawing fail.
        }
        finally
        {
            _rasterImageLoads.Remove(image.Id);
        }
    }

    private void DrawResolvedRasterImage(CanvasDrawingSession ds, RasterImageEntity image, CanvasBitmap bitmap)
    {
        if (image.ClipBoundary.Count < 3) return;
        using var clip = CreateRasterClipGeometry(ds, image.ClipBoundary);
        using var layer = ds.CreateLayer(1f, clip);

        var origin = WorldToScreen(image.InsertionPoint);
        var uEnd = WorldToScreen(new CadPoint(
            image.InsertionPoint.X + image.UVectorPerPixel.X,
            image.InsertionPoint.Y + image.UVectorPerPixel.Y));
        var vEnd = WorldToScreen(new CadPoint(
            image.InsertionPoint.X + image.VVectorPerPixel.X,
            image.InsertionPoint.Y + image.VVectorPerPixel.Y));
        var u = uEnd - origin;
        var v = vEnd - origin;
        var transform = new Matrix3x2(u.X, u.Y, v.X, v.Y, origin.X, origin.Y);
        var previous = ds.Transform;
        try
        {
            ds.Transform = transform;
            var destination = new Rect(0, 0, image.PixelWidth, image.PixelHeight);
            var opacity = Math.Clamp(1f - (image.Fade / 100f), 0f, 1f);
            ds.DrawImage(bitmap, destination, bitmap.Bounds, opacity);
        }
        finally
        {
            ds.Transform = previous;
        }
    }

    private CanvasGeometry CreateRasterClipGeometry(CanvasDrawingSession ds, IReadOnlyList<CadPoint> boundary)
    {
        using var builder = new CanvasPathBuilder(ds.Device);
        builder.BeginFigure(WorldToScreen(boundary[0]));
        for (var i = 1; i < boundary.Count; i++) builder.AddLine(WorldToScreen(boundary[i]));
        builder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(builder);
    }

    private void DrawRasterReferenceFrame(CanvasDrawingSession ds, RasterImageEntity image, Color color, float strokeWidth)
    {
        if (image.ClipBoundary.Count < 2) return;
        var points = image.ClipBoundary.Select(WorldToScreen).ToArray();
        for (var i = 1; i < points.Length; i++) ds.DrawLine(points[i - 1], points[i], color, strokeWidth);
        ds.DrawLine(points[^1], points[0], color, strokeWidth);

        using var format = new CanvasTextFormat { FontSize = 10, WordWrapping = CanvasWordWrapping.NoWrap };
        var label = image.IsResolved ? "IMAGE" : $"IMAGE: {Path.GetFileName(image.ReferencePath)}";
        ds.DrawText(label, points[0] + new Vector2(4, -14), color, format);
    }

    private void DisposeRasterImageResources()
    {
        foreach (var bitmap in _rasterImageBitmaps.Values) bitmap.Dispose();
        _rasterImageBitmaps.Clear();
        _rasterImageLoads.Clear();
    }
}
