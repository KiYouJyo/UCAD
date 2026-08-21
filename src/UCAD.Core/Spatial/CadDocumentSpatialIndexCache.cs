using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Spatial;

public sealed class CadDocumentSpatialIndexCache : IDisposable
{
    private readonly object _gate = new();
    private readonly CadDocument _document;
    private readonly Func<CadDocument, IEnumerable<ICadEntity>> _entitySource;
    private CadEntitySpatialIndex? _index;
    private bool _dirty = true;
    private bool _disposed;

    public CadDocumentSpatialIndexCache(
        CadDocument document,
        Func<CadDocument, IEnumerable<ICadEntity>>? entitySource = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entitySource = entitySource ?? (current => current.Entities);
        _document.Changed += Document_Changed;
    }

    public int RebuildCount { get; private set; }

    public IReadOnlyList<ICadEntity> Query(CadRect rectangle) => EnsureIndex().Query(rectangle);

    public ICadEntity? FindNearest(CadPoint point, double maximumDistance) =>
        EnsureIndex().FindNearest(point, maximumDistance);

    public void Invalidate()
    {
        ThrowIfDisposed();
        lock (_gate) _dirty = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _document.Changed -= Document_Changed;
        lock (_gate)
        {
            _index = null;
            _dirty = true;
        }
    }

    private void Document_Changed(object? sender, CadDocumentChangedEventArgs e)
    {
        if (_disposed) return;
        lock (_gate) _dirty = true;
    }

    private CadEntitySpatialIndex EnsureIndex()
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (!_dirty && _index is not null) return _index;
            _index = CadEntitySpatialIndex.Build(_entitySource(_document).ToArray());
            _dirty = false;
            RebuildCount++;
            return _index;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CadDocumentSpatialIndexCache));
    }
}
