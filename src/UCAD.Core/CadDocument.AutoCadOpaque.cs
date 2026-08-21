using UCAD.Core.IO;

namespace UCAD.Core;

public sealed partial class CadDocument
{
    private CadAutoCadSourceEnvelope? _autoCadSourceEnvelope;

    /// <summary>
    /// Original imported AutoCAD container retained for lossless recovery of unsupported/custom data.
    /// It is interoperability metadata and is not part of geometry Undo/Redo.
    /// </summary>
    public CadAutoCadSourceEnvelope? AutoCadSourceEnvelope => _autoCadSourceEnvelope;

    /// <summary>
    /// Attaches an original AutoCAD source container without marking the drawing as edited. The envelope
    /// is rebased to the current document revision so later edits can be distinguished from import-time
    /// semantic construction.
    /// </summary>
    public void AttachAutoCadSourceEnvelope(CadAutoCadSourceEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _autoCadSourceEnvelope = envelope.Rebase(Revision);
    }

    public void ClearAutoCadSourceEnvelope() => _autoCadSourceEnvelope = null;
}
