using UCAD.Core.Layout;

namespace UCAD.Core;

public sealed partial class CadDocument
{
    private readonly List<CadLayoutDefinition> _layouts = [new CadLayoutDefinition("Layout1")];
    private string _activeLayoutName = "Layout1";

    public IReadOnlyList<CadLayoutDefinition> Layouts => _layouts;
    public string ActiveLayoutName => _activeLayoutName;
    public CadLayoutDefinition ActiveLayout => GetLayout(_activeLayoutName);
    public CadPageSetup ActivePageSetup => ActiveLayout.PageSetup;

    public CadLayoutDefinition GetLayout(string name) =>
        _layouts.FirstOrDefault(layout => string.Equals(layout.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Layout '{name}' does not exist.");

    public bool TryGetLayout(string name, out CadLayoutDefinition? layout)
    {
        layout = _layouts.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return layout is not null;
    }

    /// <summary>
    /// Replaces the complete paper-layout table as one document mutation. This is used by
    /// the WinUI layout controller and by the native codec. Layout-table changes currently
    /// participate in dirty/revision tracking but intentionally do not consume geometry Undo.
    /// </summary>
    public bool SetLayoutTable(IEnumerable<CadLayoutDefinition> layouts, string activeLayoutName)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        if (string.IsNullOrWhiteSpace(activeLayoutName)) throw new ArgumentException("Active layout name cannot be empty.", nameof(activeLayoutName));
        var snapshot = layouts.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("A document requires at least one paper layout.", nameof(layouts));
        if (snapshot.Any(layout => layout is null)) throw new ArgumentException("Layout table cannot contain null values.", nameof(layouts));
        if (snapshot.Select(layout => layout.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Length)
            throw new ArgumentException("Layout names must be unique.", nameof(layouts));
        var active = snapshot.FirstOrDefault(layout => string.Equals(layout.Name, activeLayoutName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Active layout '{activeLayoutName}' does not exist in the supplied table.", nameof(activeLayoutName));

        if (LayoutTablesEqual(_layouts, snapshot) && string.Equals(_activeLayoutName, active.Name, StringComparison.Ordinal)) return false;

        _layouts.Clear();
        _layouts.AddRange(snapshot);
        _activeLayoutName = active.Name;
        RaiseChanged(CadDocumentChangeKind.LayoutTable);
        return true;
    }

    public bool SetActiveLayout(string name)
    {
        var layout = GetLayout(name);
        if (string.Equals(_activeLayoutName, layout.Name, StringComparison.Ordinal)) return false;
        _activeLayoutName = layout.Name;
        RaiseChanged(CadDocumentChangeKind.LayoutTable);
        return true;
    }

    public bool ReplaceLayout(CadLayoutDefinition layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var index = _layouts.FindIndex(existing => string.Equals(existing.Name, layout.Name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new KeyNotFoundException($"Layout '{layout.Name}' does not exist.");
        if (LayoutEquals(_layouts[index], layout)) return false;
        _layouts[index] = layout;
        RaiseChanged(CadDocumentChangeKind.LayoutTable);
        return true;
    }

    private static bool LayoutTablesEqual(IReadOnlyList<CadLayoutDefinition> first, IReadOnlyList<CadLayoutDefinition> second)
    {
        if (first.Count != second.Count) return false;
        for (var i = 0; i < first.Count; i++)
            if (!LayoutEquals(first[i], second[i])) return false;
        return true;
    }

    private static bool LayoutEquals(CadLayoutDefinition first, CadLayoutDefinition second)
    {
        if (!string.Equals(first.Name, second.Name, StringComparison.Ordinal) ||
            first.ModelLayout != second.ModelLayout ||
            first.PageSetup != second.PageSetup ||
            first.Viewports.Count != second.Viewports.Count)
            return false;

        for (var i = 0; i < first.Viewports.Count; i++)
            if (first.Viewports[i] != second.Viewports[i]) return false;
        return true;
    }
}
