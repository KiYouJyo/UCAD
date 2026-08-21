using UCAD.Core.Geometry;
using UCAD.Core.Styles;

namespace UCAD.Core.Entities;

public sealed record TextEntity : ICadEntity
{
    public TextEntity(CadPoint position, string text, double height = 2.5, double rotationRadians = 0)
        : this(position, text, height, rotationRadians, CadTextStyle.DefaultName, Guid.NewGuid()) { }

    public TextEntity(CadPoint position, string text, double height, double rotationRadians, string styleName)
        : this(position, text, height, rotationRadians, styleName, Guid.NewGuid()) { }

    internal TextEntity(CadPoint position, string text, double height, double rotationRadians, Guid id)
        : this(position, text, height, rotationRadians, CadTextStyle.DefaultName, id) { }

    internal TextEntity(CadPoint position, string text, double height, double rotationRadians, string styleName, Guid id)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Text cannot be empty.", nameof(text));
        if (!double.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));
        if (string.IsNullOrWhiteSpace(styleName)) throw new ArgumentException("Text style cannot be empty.", nameof(styleName));
        Position = position;
        Text = text;
        Height = height;
        RotationRadians = rotationRadians;
        StyleName = styleName.Trim();
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Position { get; }
    public string Text { get; }
    public double Height { get; }
    public double RotationRadians { get; }
    public string StyleName { get; }
    public double ApproximateWidth => Math.Max(Height * 0.6, Text.Length * Height * 0.6);
}