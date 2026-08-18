namespace UCAD.Core;

public enum CadDocumentChangeKind
{
    Add,
    AddRange,
    Remove,
    RemoveRange,
    Replace,
    ReplaceRange,
    CompoundEdit,
    Clear,
    LayerTable,
    CurrentLayer,
    EntityProperties,
    BlockTable,
    StyleTable,
    Undo,
    Redo
}

public sealed class CadDocumentChangedEventArgs : EventArgs
{
    public CadDocumentChangedEventArgs(CadDocumentChangeKind kind, int entityCount, long revision)
    {
        Kind = kind;
        EntityCount = entityCount;
        Revision = revision;
    }

    public CadDocumentChangeKind Kind { get; }
    public int EntityCount { get; }
    public long Revision { get; }
}