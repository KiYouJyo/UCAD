using UCAD.Core.Entities;

namespace UCAD.Core;

public sealed class CadDocument
{
    private readonly List<ICadEntity> _entities = [];
    private readonly Stack<ICadEntity[]> _undo = new();
    private readonly Stack<ICadEntity[]> _redo = new();

    public IReadOnlyList<ICadEntity> Entities => _entities;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Add(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        RecordMutation();
        _entities.Add(entity);
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

        RecordMutation();
        _entities.AddRange(additions);
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
        return true;
    }

    public void Clear()
    {
        if (_entities.Count == 0)
        {
            return;
        }

        RecordMutation();
        _entities.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        _redo.Push(_entities.ToArray());
        Restore(_undo.Pop());
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
}
