using UCAD.Core.Geometry;

namespace UCAD.Core.Blocks;

public sealed record CadBlockAttributeDefinition
{
    public CadBlockAttributeDefinition(
        string tag,
        string prompt,
        string defaultValue,
        CadPoint position,
        double textHeight = 2.5,
        bool constant = false)
    {
        if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Attribute tag cannot be empty.", nameof(tag));
        if (tag.Any(char.IsWhiteSpace)) throw new ArgumentException("Attribute tag cannot contain whitespace.", nameof(tag));
        if (!double.IsFinite(textHeight) || textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
        Tag = tag.Trim().ToUpperInvariant();
        Prompt = string.IsNullOrWhiteSpace(prompt) ? Tag : prompt.Trim();
        DefaultValue = defaultValue ?? string.Empty;
        Position = position;
        TextHeight = textHeight;
        Constant = constant;
    }

    public string Tag { get; }
    public string Prompt { get; }
    public string DefaultValue { get; }
    public CadPoint Position { get; }
    public double TextHeight { get; }
    public bool Constant { get; }
}