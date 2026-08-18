using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record MTextEntity : ICadEntity
{
    public MTextEntity(
        CadPoint position,
        string text,
        double textHeight = 2.5,
        double width = 40,
        double rotationRadians = 0,
        string styleName = "Standard")
        : this(position, text, textHeight, width, rotationRadians, styleName, Guid.NewGuid()) { }

    internal MTextEntity(
        CadPoint position,
        string text,
        double textHeight,
        double width,
        double rotationRadians,
        string styleName,
        Guid id)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("MText cannot be empty.", nameof(text));
        if (!double.IsFinite(textHeight) || textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
        if (!double.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));
        if (string.IsNullOrWhiteSpace(styleName)) throw new ArgumentException("Text style cannot be empty.", nameof(styleName));
        Position = position;
        Text = text;
        TextHeight = textHeight;
        Width = width;
        RotationRadians = rotationRadians;
        StyleName = styleName.Trim();
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Position { get; }
    public string Text { get; }
    public double TextHeight { get; }
    public double Width { get; }
    public double RotationRadians { get; }
    public string StyleName { get; }

    public IReadOnlyList<string> ApproximateLines()
    {
        var maxCharacters = Math.Max(1, (int)Math.Floor(Width / (TextHeight * 0.6)));
        var result = new List<string>();
        foreach (var paragraph in Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }
            for (var index = 0; index < paragraph.Length; index += maxCharacters)
                result.Add(paragraph.Substring(index, Math.Min(maxCharacters, paragraph.Length - index)));
        }
        return result;
    }
}