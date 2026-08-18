using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System.Collections.Concurrent;
using UCAD.Core.Geometry;
using UCAD.Core.Plot;
using UCAD.Core.Styles;

namespace UCAD.Services;

/// <summary>
/// Converts DirectWrite/Win2D text using installed Windows fonts into a normalized vector
/// mesh. No font bytes are embedded or distributed: the geometry is tessellated locally
/// at export time and handed to the pure Core PDF writer as closed vector figures.
/// </summary>
public sealed class Win2dPdfTextOutlineProvider : ICadPdfTextOutlineProvider
{
    private const float EmSize = 1024f;
    private readonly ConcurrentDictionary<CacheKey, CadPdfTextOutline?> _cache = new();

    public bool TryCreateOutline(
        string text,
        CadTextStyle style,
        out CadPdfTextOutline? outline,
        out string? warning)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (string.IsNullOrEmpty(text))
        {
            outline = null;
            warning = null;
            return false;
        }

        var key = new CacheKey(text, style.FontFamily, style.WidthFactor, style.ObliqueAngleDegrees);
        try
        {
            outline = _cache.GetOrAdd(key, static value => Build(value));
            warning = outline is null
                ? "The installed Windows font stack produced no drawable glyph outline."
                : null;
            return outline is not null;
        }
        catch (Exception ex)
        {
            outline = null;
            warning = $"Windows text outline generation failed: {ex.Message}";
            return false;
        }
    }

    private static CadPdfTextOutline? Build(CacheKey key)
    {
        using var format = new CanvasTextFormat
        {
            FontFamily = key.FontFamily,
            FontSize = EmSize,
            WordWrapping = CanvasWordWrapping.NoWrap
        };
        var device = CanvasDevice.GetSharedDevice();
        using var layout = new CanvasTextLayout(device, key.Text, format, 1_000_000, 1_000_000);
        using var geometry = CanvasGeometry.CreateText(layout);
        var triangles = geometry.Tessellate();
        if (triangles.Length == 0) return null;

        var shear = Math.Tan(key.ObliqueAngleDegrees * Math.PI / 180.0);
        CadPoint Normalize(System.Numerics.Vector2 point)
        {
            var x = point.X / EmSize;
            var y = point.Y / EmSize;
            return new CadPoint((x - (shear * y)) * key.WidthFactor, y);
        }

        var figures = triangles.Select(triangle => new CadPdfTextOutlineFigure(
            [Normalize(triangle.Vertex1), Normalize(triangle.Vertex2), Normalize(triangle.Vertex3)],
            closed: true)).ToArray();
        return new CadPdfTextOutline(figures);
    }

    private readonly record struct CacheKey(
        string Text,
        string FontFamily,
        double WidthFactor,
        double ObliqueAngleDegrees);
}
