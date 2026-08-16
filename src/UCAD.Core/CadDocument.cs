using UCAD.Core.Entities;
using UCAD.Core.Layers;

namespace UCAD.Core;

public sealed class CadDocument
{
    private readonly List<ICadEntity> _entities = [];
    private readonly List<CadLayer> _layers = [CadLayer.CreateDefault()];
    private readonly Dictionary<Guid, CadEntityProperties> _entityProperties = [];
    private readonly Stack<DocumentSnapshot> _undo = new();
    private readonly Stack<DocumentSnapshot> _redo = new();
    private string _currentLayerName = CadLayer.DefaultLayerName;

    public event EventHandler<CadDocumentChangedEventArgs>? Changed;

    public IReadOnlyList<ICadEntity> Entities => _entities;
    public IReadOnlyList<CadLayer> Layers => _layers;
    public string CurrentLayerName => _currentLayerName;

    public IEnumerable<ICadEntity> VisibleEntities =>
        _entities.Where(entity => GetLayerForEntity(entity.Id).IsVisible);

    public IEnumerable<ICadEntity> SelectableEntities =>
        _entities.Where(entity =>
        {
            var layer = GetLayerForEntity(entity.Id);
            return layer.IsVisible && !layer.IsLocked;
        });

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public long Revision { get; private set; }

    public CadLayer GetLayer(string name) =>
        _layers.FirstOrDefault(layer => string.Equals(layer.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Layer '{name}' does not exist.");

    public bool TryGetLayer(string name, out CadLayer? layer)
    {
        layer = _layers.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return layer is not null;
    }

    public CadEntityProperties GetEntityProperties(Guid id)
    {
        if (_entityProperties.TryGetValue(id, out var properties))
        {
            return properties;
        }
        if (_entities.Any(entity => entity.Id == id))
        {
            return new CadEntityProperties(_currentLayerName);
        }
        throw new KeyNotFoundException($"CAD entity '{id}' does not exist.");
    }

    public CadLayer GetLayerForEntity(Guid id) => GetLayer(GetEntityProperties(id).LayerName);

    public void Add(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureNoDuplicateIds([entity], excludingIds: []);
        RecordMutation();
        _entities.Add(entity);
        _entityProperties[entity.Id] = new CadEntityProperties(_currentLayerName);
        RaiseChanged(CadDocumentChangeKind.Add);
    }

    public void Add(ICadEntity entity, CadEntityProperties properties)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(properties);
        EnsureLayerExists(properties.LayerName);
        EnsureNoDuplicateIds([entity], excludingIds: []);
        RecordMutation();
        _entities.Add(entity);
        _entityProperties[entity.Id] = properties;
        RaiseChanged(CadDocumentChangeKind.Add);
    }

    public void AddRange(IEnumerable<ICadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var additions = entities.ToArray();
        if (additions.Length == 0) return;
        if (additions.Any(entity => entity is null))
            throw new ArgumentException("Entity collection cannot contain null values.", nameof(entities));

        EnsureNoDuplicateIds(additions, excludingIds: []);
        RecordMutation();
        _entities.AddRange(additions);
        foreach (var entity in additions)
            _entityProperties[entity.Id] = new CadEntityProperties(_currentLayerName);
        RaiseChanged(CadDocumentChangeKind.AddRange);
    }

    public void AddRange(IEnumerable<(ICadEntity Entity, CadEntityProperties Properties)> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var additions = entities.ToArray();
        if (additions.Length == 0) return;
        if (additions.Any(item => item.Entity is null || item.Properties is null))
            throw new ArgumentException("Entity/property collection cannot contain null values.", nameof(entities));
        foreach (var item in additions) EnsureLayerExists(item.Properties.LayerName);
        EnsureNoDuplicateIds(additions.Select(item => item.Entity), excludingIds: []);

        RecordMutation();
        foreach (var item in additions)
        {
            _entities.Add(item.Entity);
            _entityProperties[item.Entity.Id] = item.Properties;
        }
        RaiseChanged(CadDocumentChangeKind.AddRange);
    }

    public bool Remove(Guid id)
    {
        var index = _entities.FindIndex(entity => entity.Id == id);
        if (index < 0) return false;
        RecordMutation();
        _entities.RemoveAt(index);
        _entityProperties.Remove(id);
        RaiseChanged(CadDocumentChangeKind.Remove);
        return true;
    }

    public int RemoveRange(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var requested = ids.ToHashSet();
        if (requested.Count == 0) return 0;
        var removedCount = _entities.Count(entity => requested.Contains(entity.Id));
        if (removedCount == 0) return 0;

        RecordMutation();
        _entities.RemoveAll(entity => requested.Contains(entity.Id));
        foreach (var id in requested) _entityProperties.Remove(id);
        RaiseChanged(CadDocumentChangeKind.RemoveRange);
        return removedCount;
    }

    /// <summary>
    /// Replaces one entity with zero, one, or many entities as one undoable mutation.
    /// New split pieces inherit the source entity's layer and display properties.
    /// </summary>
    public bool Replace(Guid id, IEnumerable<ICadEntity> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var replacementSnapshot = replacements.ToArray();
        if (replacementSnapshot.Any(entity => entity is null))
            throw new ArgumentException("Replacement collection cannot contain null values.", nameof(replacements));

        var index = _entities.FindIndex(entity => entity.Id == id);
        if (index < 0) return false;
        EnsureNoDuplicateIds(replacementSnapshot, [id]);
        var inherited = GetEntityProperties(id);

        RecordMutation();
        _entities.RemoveAt(index);
        _entityProperties.Remove(id);
        _entities.InsertRange(index, replacementSnapshot);
        foreach (var replacement in replacementSnapshot)
            _entityProperties[replacement.Id] = inherited;
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
        if (snapshot.Length == 0) return 0;
        if (snapshot.Any(entity => entity is null))
            throw new ArgumentException("Replacement collection cannot contain null values.", nameof(replacements));

        var byId = snapshot.ToDictionary(entity => entity.Id);
        var matched = _entities.Count(entity => byId.ContainsKey(entity.Id));
        if (matched == 0) return 0;
        if (matched != byId.Count)
            throw new ArgumentException("Every replacement must match an existing entity Id.", nameof(replacements));

        RecordMutation();
        for (var i = 0; i < _entities.Count; i++)
        {
            if (byId.TryGetValue(_entities[i].Id, out var replacement))
                _entities[i] = replacement;
        }
        RaiseChanged(CadDocumentChangeKind.ReplaceRange);
        return matched;
    }

    public void Clear()
    {
        if (_entities.Count == 0) return;
        RecordMutation();
        _entities.Clear();
        _entityProperties.Clear();
        RaiseChanged(CadDocumentChangeKind.Clear);
    }

    public void CreateLayer(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (TryGetLayer(layer.Name, out _))
            throw new InvalidOperationException($"Layer '{layer.Name}' already exists.");
        RecordMutation();
        _layers.Add(layer);
        RaiseChanged(CadDocumentChangeKind.LayerTable);
    }

    public bool DeleteLayer(string name)
    {
        if (string.Equals(name, CadLayer.DefaultLayerName, StringComparison.OrdinalIgnoreCase))
            return false;
        var layer = _layers.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (layer is null) return false;

        RecordMutation();
        _layers.Remove(layer);
        foreach (var id in _entityProperties.Where(pair => string.Equals(pair.Value.LayerName, layer.Name, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray())
            _entityProperties[id] = _entityProperties[id] with { LayerName = CadLayer.DefaultLayerName };
        if (string.Equals(_currentLayerName, layer.Name, StringComparison.OrdinalIgnoreCase))
            _currentLayerName = CadLayer.DefaultLayerName;
        RaiseChanged(CadDocumentChangeKind.LayerTable);
        return true;
    }

    public void RenameLayer(string oldName, string newName)
    {
        if (string.Equals(oldName, CadLayer.DefaultLayerName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Layer 0 cannot be renamed.");
        if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Layer name cannot be empty.", nameof(newName));
        var layer = GetLayer(oldName);
        if (TryGetLayer(newName, out _)) throw new InvalidOperationException($"Layer '{newName}' already exists.");
        var trimmed = newName.Trim();

        RecordMutation();
        var index = _layers.IndexOf(layer);
        _layers[index] = layer with { Name = trimmed };
        foreach (var id in _entityProperties.Where(pair => string.Equals(pair.Value.LayerName, oldName, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray())
            _entityProperties[id] = _entityProperties[id] with { LayerName = trimmed };
        if (string.Equals(_currentLayerName, oldName, StringComparison.OrdinalIgnoreCase)) _currentLayerName = trimmed;
        RaiseChanged(CadDocumentChangeKind.LayerTable);
    }

    public void UpdateLayer(string name, string? colorHex = null, double? lineWeight = null, string? lineType = null, bool? isVisible = null, bool? isLocked = null)
    {
        var layer = GetLayer(name);
        var updated = new CadLayer(
            layer.Name,
            colorHex ?? layer.ColorHex,
            lineWeight ?? layer.LineWeight,
            lineType ?? layer.LineType,
            isVisible ?? layer.IsVisible,
            isLocked ?? layer.IsLocked);
        if (updated == layer) return;
        RecordMutation();
        _layers[_layers.IndexOf(layer)] = updated;
        RaiseChanged(CadDocumentChangeKind.LayerTable);
    }

    public void SetCurrentLayer(string name)
    {
        EnsureLayerExists(name);
        var canonical = GetLayer(name).Name;
        if (string.Equals(_currentLayerName, canonical, StringComparison.Ordinal)) return;
        RecordMutation();
        _currentLayerName = canonical;
        RaiseChanged(CadDocumentChangeKind.CurrentLayer);
    }

    public int SetEntityProperties(IEnumerable<Guid> ids, Func<CadEntityProperties, CadEntityProperties> updater)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(updater);
        var requested = ids.ToHashSet();
        var existingIds = _entities.Where(entity => requested.Contains(entity.Id)).Select(entity => entity.Id).ToArray();
        if (existingIds.Length == 0) return 0;

        var updates = new Dictionary<Guid, CadEntityProperties>();
        foreach (var id in existingIds)
        {
            var updated = updater(GetEntityProperties(id)) ?? throw new InvalidOperationException("Property updater returned null.");
            EnsureLayerExists(updated.LayerName);
            updates[id] = updated;
        }
        if (updates.All(pair => pair.Value == GetEntityProperties(pair.Key))) return 0;

        RecordMutation();
        foreach (var pair in updates) _entityProperties[pair.Key] = pair.Value;
        RaiseChanged(CadDocumentChangeKind.EntityProperties);
        return updates.Count;
    }

    public int SetEntitiesLayer(IEnumerable<Guid> ids, string layerName)
    {
        EnsureLayerExists(layerName);
        var canonical = GetLayer(layerName).Name;
        return SetEntityProperties(ids, properties => properties with { LayerName = canonical });
    }

    public int SetEntitiesColor(IEnumerable<Guid> ids, string? colorHex)
    {
        if (colorHex is not null && !CadLayer.IsValidColor(colorHex))
            throw new ArgumentException("Color must use #RRGGBB format or null for ByLayer.", nameof(colorHex));
        return SetEntityProperties(ids, properties => properties with { ColorHex = colorHex?.ToUpperInvariant() });
    }

    public int SetEntitiesLineWeight(IEnumerable<Guid> ids, double? lineWeight)
    {
        if (lineWeight is not null && (!double.IsFinite(lineWeight.Value) || lineWeight.Value <= 0))
            throw new ArgumentOutOfRangeException(nameof(lineWeight));
        return SetEntityProperties(ids, properties => properties with { LineWeight = lineWeight });
    }

    public int SetEntitiesLineType(IEnumerable<Guid> ids, string lineType)
    {
        if (string.IsNullOrWhiteSpace(lineType)) throw new ArgumentException("Line type cannot be empty.", nameof(lineType));
        return SetEntityProperties(ids, properties => properties with { LineType = lineType.Trim() });
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Push(Capture());
        Restore(_undo.Pop());
        RaiseChanged(CadDocumentChangeKind.Undo);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Push(Capture());
        Restore(_redo.Pop());
        RaiseChanged(CadDocumentChangeKind.Redo);
        return true;
    }

    private void RecordMutation()
    {
        _undo.Push(Capture());
        _redo.Clear();
    }

    private DocumentSnapshot Capture() => new(
        _entities.ToArray(),
        _layers.ToArray(),
        _entityProperties.ToDictionary(pair => pair.Key, pair => pair.Value),
        _currentLayerName);

    private void Restore(DocumentSnapshot snapshot)
    {
        _entities.Clear();
        _entities.AddRange(snapshot.Entities);
        _layers.Clear();
        _layers.AddRange(snapshot.Layers);
        _entityProperties.Clear();
        foreach (var pair in snapshot.EntityProperties) _entityProperties[pair.Key] = pair.Value;
        _currentLayerName = snapshot.CurrentLayerName;
    }

    private void EnsureLayerExists(string name)
    {
        if (!TryGetLayer(name, out _)) throw new KeyNotFoundException($"Layer '{name}' does not exist.");
    }

    private void EnsureNoDuplicateIds(IEnumerable<ICadEntity> candidates, IEnumerable<Guid> excludingIds)
    {
        var snapshot = candidates.ToArray();
        if (snapshot.Select(entity => entity.Id).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("CAD entity identities must be unique.", nameof(candidates));

        var excluded = excludingIds.ToHashSet();
        var existing = _entities.Where(entity => !excluded.Contains(entity.Id)).Select(entity => entity.Id).ToHashSet();
        if (snapshot.Any(entity => existing.Contains(entity.Id)))
            throw new InvalidOperationException("A CAD entity with the same identity already exists in the document.");
    }

    private void RaiseChanged(CadDocumentChangeKind kind)
    {
        Revision++;
        Changed?.Invoke(this, new CadDocumentChangedEventArgs(kind, _entities.Count, Revision));
    }

    private sealed record DocumentSnapshot(
        ICadEntity[] Entities,
        CadLayer[] Layers,
        Dictionary<Guid, CadEntityProperties> EntityProperties,
        string CurrentLayerName);
}
