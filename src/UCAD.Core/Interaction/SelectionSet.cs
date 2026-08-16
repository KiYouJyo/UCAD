using UCAD.Core.Entities;

namespace UCAD.Core.Interaction;

public sealed class SelectionSet
{
    private readonly CadDocument _document;
    private readonly HashSet<Guid> _selectedIds = [];

    public SelectionSet(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.Changed += Document_Changed;
    }

    public event EventHandler? Changed;

    public int Count => _selectedIds.Count;

    public bool IsEmpty => _selectedIds.Count == 0;

    public IReadOnlyCollection<Guid> SelectedIds => _selectedIds.ToArray();

    public IReadOnlyList<ICadEntity> SelectedEntities => _document.Entities
        .Where(entity => _selectedIds.Contains(entity.Id))
        .ToArray();

    public bool Contains(Guid id) => _selectedIds.Contains(id);

    public bool Replace(Guid id) => Replace([id]);

    public bool Replace(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var valid = ValidIds(ids);
        if (_selectedIds.SetEquals(valid))
        {
            return false;
        }
        _selectedIds.Clear();
        _selectedIds.UnionWith(valid);
        RaiseChanged();
        return true;
    }

    public bool Add(Guid id) => Add([id]);

    public bool Add(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var changed = false;
        foreach (var id in ValidIds(ids))
        {
            changed |= _selectedIds.Add(id);
        }
        if (changed)
        {
            RaiseChanged();
        }
        return changed;
    }

    public bool Remove(Guid id) => Remove([id]);

    public bool Remove(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var changed = false;
        foreach (var id in ids.Distinct())
        {
            changed |= _selectedIds.Remove(id);
        }
        if (changed)
        {
            RaiseChanged();
        }
        return changed;
    }

    public bool Toggle(Guid id)
    {
        if (!_document.Entities.Any(entity => entity.Id == id))
        {
            return false;
        }
        if (!_selectedIds.Remove(id))
        {
            _selectedIds.Add(id);
        }
        RaiseChanged();
        return true;
    }

    public bool Clear()
    {
        if (_selectedIds.Count == 0)
        {
            return false;
        }
        _selectedIds.Clear();
        RaiseChanged();
        return true;
    }

    private HashSet<Guid> ValidIds(IEnumerable<Guid> ids)
    {
        var existing = _document.Entities.Select(entity => entity.Id).ToHashSet();
        return ids.Where(existing.Contains).ToHashSet();
    }

    private void Document_Changed(object? sender, CadDocumentChangedEventArgs e)
    {
        var existing = _document.Entities.Select(entity => entity.Id).ToHashSet();
        if (_selectedIds.RemoveWhere(id => !existing.Contains(id)) > 0)
        {
            RaiseChanged();
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
