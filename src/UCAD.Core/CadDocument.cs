using UCAD.Core.Entities;

namespace UCAD.Core;

public sealed class CadDocument
{
    private readonly List<ICadEntity> _entities = [];
    private readonly Stack<ICadEntity[]> _undo = new();
    private readonly Stack<ICadEntity[]> _redo = new();

    public event EventHandler<CadDocumentChangedEventArgs>? Changed;

    public IReadOnlyList<ICadEntity> Entities => _entities;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public long Revision { get; private set; }

    public void Add(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureNoDuplicateIds([entity], excludingIds: []);
        RecordMutation();
        _entities.Add(entity);
        RaiseChanged(CadDocumentChangeKind.Add);
    }

    public void AddRange(IEnumerable<ICadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var additions = entities.ToArray();
        if (additions.Length == 0)
        {
            return;
        }

        if (additions.Any(entity => entity is null))
        {
            throw new ArgumentException("Entity collection cannot contain null values.", nameof(entities));
        }

        EnsureNoDuplicateIds(additions, excludingIds: []);
        RecordMutation();
        _entities.AddRange(additions);
        RaiseChanged(CadDocumentChangeKind.AddRange);
    }

    public bool Remove(Guid id)
    {
        var index = _entities.FindIndex(entity => entity.Id == id);
        if (index < 0)
        {
            return false;
        }

        RecordMutation();
        _entities.RemoveAt(index);
        RaiseChanged(CadDocumentChangeKind.Remove);
        return true;
    }

    public int RemoveRange(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var requested = ids.ToHashSet();
        if (requested.Count == 0)
        {
            return 0;
        }

        var removedCount = _entities.Count(entity => requested.Contains(entity.Id));
        if (removedCount == 0)
        {
            return 0;
        }

        RecordMutation();
        _entities.RemoveAll(entity => requested.Contains(entity.Id));
        RaiseChanged(CadDocumentChangeKind.RemoveRange);
        return removedCount;
    }

    /// <summary>
    /// Replaces one entity with zero, one, or many entities as one undoable mutation.
    /// The replacement sequence is inserted at the original entity's position.
    /// </summary>
    public bool Replace(Guid id, IEnumerable<ICadEntity> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var replacementSnapshot = replacements.ToArray();
        if (replacementSnapshot.Any(entity => entity is null))
        {
            throw new ArgumentException("Replacement collection cannot contain null values.", nameof(replacements));
        }

        var index = _entities.FindIndex(entity => entity.Id == id);
        if (index < 0)
        {
            return false;
        }

        EnsureNoDuplicateIds(replacementSnapshot, [id]);
        RecordMutation();
        _entities.RemoveAt(index);
        _entities.InsertRange(index, replacementSnapshot);
        RaiseChanged(CadDocumentChangeKind.Replace);
        return true;
    }

    /// <summary>
    /// Replaces existing entities by matching replacement Id values. Intended for
    /// identity-preserving transforms such as MOVE/ROTATE/SCALE/MIRROR.
    /// </summary>
    public int ReplaceRange(IEnumerable<ICadEntity> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var snapshot = replacements.ToArray();
        if (snapshot.Length == 0)
        {
            return 0;
        }
        if (snapshot.Any(entity => entity is null))
        {
            throw new ArgumentException("Replacement collection cannot contain null values.", nameof(replacements));
        }

        var byId = snapshot.ToDictionary(entity => entity.Id);
        var matched = _entities.Count(entity => byId.ContainsKey(entity.Id));
        if (matched == 0)
        {
            return 0;
        }
        if (matched != byId.Count)
        {
            throw new ArgumentException("Every replacement must match an existing entity Id.", nameof(replacements));
        }

        RecordMutation();
        for (var i = 0; i < _entities.Count; i++)
        {
            if (byId.TryGetValue(_entities[i].Id, out var replacement))
            {
                _entities[i] = replacement;
            }
        }
        RaiseChanged(CadDocumentChangeKind.ReplaceRange);
        return matched;
    }

    public void Clear()
    {
        if (_entities.Count == 0)
        {
            return;
        }

        RecordMutation();
        _entities.Clear();
        RaiseChanged(CadDocumentChangeKind.Clear);
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        _redo.Push(_entities.ToArray());
        Restore(_undo.Pop());
        RaiseChanged(CadDocumentChangeKind.Undo);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        _undo.Push(_entities.ToArray());
        Restore(_redo.Pop());
        RaiseChanged(CadDocumentChangeKind.Redo);
        return true;
    }

    private void RecordMutation()
    {
        _undo.Push(_entities.ToArray());
        _redo.Clear();
    }

    private void Restore(IEnumerable<ICadEntity> snapshot)
    {
        _entities.Clear();
        _entities.AddRange(snapshot);
    }

    private void EnsureNoDuplicateIds(IEnumerable<ICadEntity> candidates, IEnumerable<Guid> excludingIds)
    {
        var snapshot = candidates.ToArray();
        if (snapshot.Select(entity => entity.Id).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("CAD entity identities must be unique.", nameof(candidates));
        }

        var excluded = excludingIds.ToHashSet();
        var existing = _entities.Where(entity => !excluded.Contains(entity.Id)).Select(entity => entity.Id).ToHashSet();
        if (snapshot.Any(entity => existing.Contains(entity.Id)))
        {
            throw new InvalidOperationException("A CAD entity with the same identity already exists in the document.");
        }
    }

    private void RaiseChanged(CadDocumentChangeKind kind)
    {
        Revision++;
        Changed?.Invoke(this, new CadDocumentChangedEventArgs(kind, _entities.Count, Revision));
    }
}
