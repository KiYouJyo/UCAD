using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadOpaquePreservationTests
{
    [Fact]
    public void UntouchedImportedDocumentExportsExactOriginalContainer()
    {
        var original = FakeDwg("opaque-source-A");
        var document = CreateEditableDocument();
        document.AttachAutoCadSourceEnvelope(new CadAutoCadSourceEnvelope(original, ".dwg", "AC1032", proxyEntityCount: 1, preservationReasons: ["proxy entity"]));
        var exported = CadAcadPreservingInteropCodec.ExportDwg(document);
        Assert.Equal(original, exported.Content);
        Assert.Equal("AC1032", exported.TargetCadVersion);
        Assert.Empty(exported.Warnings);
    }

    [Fact]
    public void UntouchedImportedDxfExportsExactOriginalBytesIncludingUnknownPayloads()
    {
        var sourceDocument = CreateEditableDocument();
        var text = CadDxfFullInteropCodec.Export(sourceDocument).Content;
        var marker = "999\nUCAD opaque preservation marker\n";
        var insertion = text.LastIndexOf("0\nEOF", StringComparison.Ordinal);
        Assert.True(insertion >= 0);
        var original = Encoding.UTF8.GetBytes(text.Insert(insertion, marker));

        var imported = CadAcadPreservingInteropCodec.ImportDxf(original);
        var exported = CadAcadPreservingInteropCodec.ExportDxf(imported.Document, binary: false);

        Assert.Equal(original, exported.Content);
        Assert.NotNull(imported.Document.AutoCadSourceEnvelope);
        Assert.Equal(".dxf", imported.Document.AutoCadSourceEnvelope!.SourceExtension);
    }

    [Fact]
    public void EditedDocumentUsesSemanticDwgAndWarnsThatOpaqueDataWasNotMerged()
    {
        var original = FakeDwg("opaque-source-B");
        var document = CreateEditableDocument();
        document.AttachAutoCadSourceEnvelope(new CadAutoCadSourceEnvelope(original, ".dwg", "AC1032", proxyObjectCount: 1, preservationReasons: ["custom object"]));
        document.Add(new CircleEntity(new CadPoint(20, 20), 4));
        var exported = CadAcadPreservingInteropCodec.ExportDwg(document);
        Assert.NotEqual(original, exported.Content);
        Assert.True(exported.Content.Length > 6);
        Assert.Equal("AC1032", Encoding.ASCII.GetString(exported.Content.AsSpan(0, 6)));
        Assert.Contains(exported.Warnings, warning => warning.Contains("cannot yet be merged", StringComparison.OrdinalIgnoreCase) && warning.Contains("original", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EditedDxfUsesSemanticOutputAndKeepsOriginalRecoverable()
    {
        var sourceDocument = CreateEditableDocument();
        var original = Encoding.UTF8.GetBytes(CadDxfFullInteropCodec.Export(sourceDocument).Content);
        var imported = CadAcadPreservingInteropCodec.ImportDxf(original);
        imported.Document.Add(new CircleEntity(new CadPoint(25, 25), 2));

        var exported = CadAcadPreservingInteropCodec.ExportDxf(imported.Document);
        var recovery = CadAcadPreservingInteropCodec.ExportOriginalAutoCadSource(imported.Document);
        Assert.NotEqual(original, exported.Content);
        Assert.Equal(original, recovery.Content);
        Assert.Contains(exported.Warnings, warning => warning.Contains("proxy/custom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OriginalContainerRemainsRecoverableAfterDocumentEdits()
    {
        var original = FakeDwg("opaque-source-C");
        var document = CreateEditableDocument();
        document.AttachAutoCadSourceEnvelope(new CadAutoCadSourceEnvelope(original, ".dwg", "AC1032", customClassCount: 3, preservationReasons: ["ObjectARX classes"]));
        document.Add(new TextEntity(new CadPoint(3, 3), "edited", 2.5));
        var recovery = CadAcadPreservingInteropCodec.ExportOriginalAutoCadSource(document);
        Assert.Equal(original, recovery.Content);
        Assert.Equal(".dwg", recovery.TargetExtension);
        Assert.Equal("AC1032", recovery.TargetCadVersion);
    }

    [Fact]
    public void NativeUcadPersistenceRetainsAndVerifiesOpaqueSourceEnvelope()
    {
        var original = FakeDwg("opaque-source-D");
        var document = CreateEditableDocument();
        document.AttachAutoCadSourceEnvelope(new CadAutoCadSourceEnvelope(original, ".dwg", "AC1032", proxyEntityCount: 2, proxyObjectCount: 1, customClassCount: 4, preservationReasons: ["proxy entity", "custom class"]));
        var json = CadNativeDocumentCodecAutoCad.Serialize(document);
        var restored = CadNativeDocumentCodecAutoCad.Deserialize(json);
        Assert.True(CadNativeDocumentCodecAutoCad.HasAutoCadOpaqueExtension(json));
        var envelope = Assert.IsType<CadAutoCadSourceEnvelope>(restored.AutoCadSourceEnvelope);
        Assert.Equal(original, envelope.CopyContent());
        Assert.Equal(2, envelope.ProxyEntityCount);
        Assert.Equal(1, envelope.ProxyObjectCount);
        Assert.Equal(4, envelope.CustomClassCount);
        Assert.Equal(document.AutoCadSourceEnvelope!.Sha256, envelope.Sha256);
        Assert.True(envelope.IsDocumentUnmodified(restored));
    }

    private static CadDocument CreateEditableDocument()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(10, 10)));
        document.ResetHistory();
        return document;
    }

    private static byte[] FakeDwg(string marker) => Encoding.ASCII.GetBytes("AC1032\0" + marker + "\0opaque-payload");
}
