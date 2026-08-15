using UCAD.Core.Entities;

namespace UCAD.Core;

public sealed class CadDocument
{
    private readonly List<ICadEntity> _entities = [];

    public IReadOnlyList<ICadEntity> Entities => _entities;

    public void Add(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _entities.Add(entity);
    }

    public bool Remove(Guid id)
    {
        var index = _entities.FindIndex(entity => entity.Id == id);
        if (index < 0)
        {
            return false;
        }

        _entities.RemoveAt(index);
        return true;
    }

    public void Clear() => _entities.Clear();
}
