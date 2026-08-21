namespace UCAD.Core.Layout;

public sealed class CadLayoutDefinition
{
    private readonly IReadOnlyList<CadLayoutViewport> _viewports;

    public CadLayoutDefinition(
        string name,
        CadPageSetup? pageSetup = null,
        IEnumerable<CadLayoutViewport>? viewports = null,
        bool modelLayout = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Layout name cannot be empty.", nameof(name));
        var snapshot = viewports?.ToArray() ?? [];
        if (snapshot.Select(viewport => viewport.Id).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Layout viewport identities must be unique.", nameof(viewports));
        Name = name.Trim();
        PageSetup = pageSetup ?? new CadPageSetup();
        _viewports = Array.AsReadOnly(snapshot);
        ModelLayout = modelLayout;
    }

    public string Name { get; }
    public CadPageSetup PageSetup { get; }
    public IReadOnlyList<CadLayoutViewport> Viewports => _viewports;
    public bool ModelLayout { get; }

    public CadLayoutDefinition WithPageSetup(CadPageSetup pageSetup) =>
        new(Name, pageSetup ?? throw new ArgumentNullException(nameof(pageSetup)), Viewports, ModelLayout);

    public CadLayoutDefinition AddViewport(CadLayoutViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        if (Viewports.Any(existing => existing.Id == viewport.Id))
            throw new InvalidOperationException("A viewport with the same identity already exists in this layout.");
        return new CadLayoutDefinition(Name, PageSetup, Viewports.Append(viewport), ModelLayout);
    }

    public CadLayoutDefinition ReplaceViewport(CadLayoutViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        if (!Viewports.Any(existing => existing.Id == viewport.Id))
            throw new KeyNotFoundException("Viewport does not exist in this layout.");
        return new CadLayoutDefinition(
            Name,
            PageSetup,
            Viewports.Select(existing => existing.Id == viewport.Id ? viewport : existing),
            ModelLayout);
    }

    public CadLayoutDefinition RemoveViewport(Guid viewportId) =>
        new(Name, PageSetup, Viewports.Where(viewport => viewport.Id != viewportId), ModelLayout);

    public CadLayoutDefinition Rename(string newName) =>
        new(newName, PageSetup, Viewports, ModelLayout);
}