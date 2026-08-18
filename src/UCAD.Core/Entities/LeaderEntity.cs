using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed class LeaderEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _points;

    public LeaderEntity(
        IEnumerable<CadPoint> points,
        string text,
        double textHeight = 2.5,
        string styleName = "Standard")
        : this(points, text, textHeight, styleName, Guid.NewGuid()) { }

    internal LeaderEntity(
        IEnumerable<CadPoint> points,
        string text,
        double textHeight,
        string styleName,
        Guid id)
    {
        ArgumentNullException.ThrowIfNull(points);
        var snapshot = points.ToArray();
        if (snapshot.Length < 2) throw new ArgumentException("A leader requires at least two points.", nameof(points));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Leader text cannot be empty.", nameof(text));
        if (!double.IsFinite(textHeight) || textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
        if (string.IsNullOrWhiteSpace(styleName)) throw new ArgumentException("Leader style cannot be empty.", nameof(styleName));
        _points = Array.AsReadOnly(snapshot);
        Text = text;
        TextHeight = textHeight;
        StyleName = styleName.Trim();
        Id = id;
    }

    public Guid Id { get; }
    public IReadOnlyList<CadPoint> Points => _points;
    public string Text { get; }
    public double TextHeight { get; }
    public string StyleName { get; }
}