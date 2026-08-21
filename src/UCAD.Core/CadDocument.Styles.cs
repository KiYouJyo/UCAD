using UCAD.Core.Styles;

namespace UCAD.Core;

public sealed partial class CadDocument
{
    private readonly List<CadTextStyle> _textStyles = [CadTextStyle.CreateDefault()];
    private readonly List<CadDimensionStyle> _dimensionStyles = [CadDimensionStyle.CreateDefault()];
    private string _currentTextStyleName = CadTextStyle.DefaultName;
    private string _currentDimensionStyleName = CadDimensionStyle.DefaultName;

    public IReadOnlyList<CadTextStyle> TextStyles => _textStyles;
    public IReadOnlyList<CadDimensionStyle> DimensionStyles => _dimensionStyles;
    public string CurrentTextStyleName => _currentTextStyleName;
    public string CurrentDimensionStyleName => _currentDimensionStyleName;

    public CadTextStyle GetTextStyle(string name) =>
        _textStyles.FirstOrDefault(style => string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Text style '{name}' does not exist.");

    public CadDimensionStyle GetDimensionStyle(string name) =>
        _dimensionStyles.FirstOrDefault(style => string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Dimension style '{name}' does not exist.");

    public bool TryGetTextStyle(string name, out CadTextStyle? style)
    {
        style = _textStyles.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return style is not null;
    }

    public bool TryGetDimensionStyle(string name, out CadDimensionStyle? style)
    {
        style = _dimensionStyles.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return style is not null;
    }

    public void DefineTextStyle(CadTextStyle style, bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(style);
        var existing = _textStyles.FirstOrDefault(candidate => string.Equals(candidate.Name, style.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null && !replaceExisting) throw new InvalidOperationException($"Text style '{style.Name}' already exists.");
        if (existing == style) return;
        RecordMutation();
        if (existing is null) _textStyles.Add(style);
        else _textStyles[_textStyles.IndexOf(existing)] = style;
        RaiseChanged(CadDocumentChangeKind.StyleTable);
    }

    public void DefineDimensionStyle(CadDimensionStyle style, bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(style);
        var existing = _dimensionStyles.FirstOrDefault(candidate => string.Equals(candidate.Name, style.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null && !replaceExisting) throw new InvalidOperationException($"Dimension style '{style.Name}' already exists.");
        if (existing == style) return;
        RecordMutation();
        if (existing is null) _dimensionStyles.Add(style);
        else _dimensionStyles[_dimensionStyles.IndexOf(existing)] = style;
        RaiseChanged(CadDocumentChangeKind.StyleTable);
    }

    public void SetCurrentTextStyle(string name)
    {
        var canonical = GetTextStyle(name).Name;
        if (string.Equals(_currentTextStyleName, canonical, StringComparison.Ordinal)) return;
        RecordMutation();
        _currentTextStyleName = canonical;
        RaiseChanged(CadDocumentChangeKind.StyleTable);
    }

    public void SetCurrentDimensionStyle(string name)
    {
        var canonical = GetDimensionStyle(name).Name;
        if (string.Equals(_currentDimensionStyleName, canonical, StringComparison.Ordinal)) return;
        RecordMutation();
        _currentDimensionStyleName = canonical;
        RaiseChanged(CadDocumentChangeKind.StyleTable);
    }

    public bool DeleteTextStyle(string name)
    {
        if (string.Equals(name, CadTextStyle.DefaultName, StringComparison.OrdinalIgnoreCase)) return false;
        var existing = _textStyles.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return false;
        RecordMutation();
        _textStyles.Remove(existing);
        if (string.Equals(_currentTextStyleName, existing.Name, StringComparison.OrdinalIgnoreCase)) _currentTextStyleName = CadTextStyle.DefaultName;
        RaiseChanged(CadDocumentChangeKind.StyleTable);
        return true;
    }

    public bool DeleteDimensionStyle(string name)
    {
        if (string.Equals(name, CadDimensionStyle.DefaultName, StringComparison.OrdinalIgnoreCase)) return false;
        var existing = _dimensionStyles.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return false;
        RecordMutation();
        _dimensionStyles.Remove(existing);
        if (string.Equals(_currentDimensionStyleName, existing.Name, StringComparison.OrdinalIgnoreCase)) _currentDimensionStyleName = CadDimensionStyle.DefaultName;
        RaiseChanged(CadDocumentChangeKind.StyleTable);
        return true;
    }
}