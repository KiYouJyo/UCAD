using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record TextEntity : ICadEntity
{
    public TextEntity(CadPoint position, string text, double height = 2.5, double rotationRadians = 0)
        : this(position, text, height, rotationRadians, Guid.NewGuid())
    {
    }

    internal TextEntity(CadPoint position, string text, double height, double rotationRadians, Guid id)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Text cannot be empty.", nameof(text));
        if (!double.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));
        Position = position;
        Text = text;
        Height = height;
        RotationRadians = rotationRadians;
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Position { get; }
    public string Text { get; }
    public double Height { get; }
    public double RotationRadians { get; }
    public double ApproximateWidth => Math.Max(Height * 0.6, Text.Length * Height * 0.6);
}
