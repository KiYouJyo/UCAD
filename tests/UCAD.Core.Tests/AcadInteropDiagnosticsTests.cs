using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadInteropDiagnosticsTests
{
    [Theory]
    [InlineData("DWG read: Unlisted object with DXF name WIPEOUTVARIABLES has been read as an UnknownNonGraphicalObject")]
    [InlineData("DWG read: Unlisted object with DXF name TCH_DBCONFIG has been read as an UnknownNonGraphicalObject")]
    [InlineData("DWG read: Unlisted object with DXF name ACDBDETAILVIEWSTYLE has been read as an UnknownNonGraphicalObject")]
    [InlineData("DWG read: Unlisted object with DXF name ACDBSECTIONVIEWSTYLE has been read as an UnknownNonGraphicalObject")]
    [InlineData("DWG read: Entry not found ACAD_WIPEOUT_VARS|587 for dictionary |12")]
    [InlineData("DWG read: Entry not found TCH_DBCONFIG|563 for dictionary |12")]
    [InlineData("DWG read: Entry not found Imperial24|603 for dictionary ACAD_DETAILVIEWSTYLE|602")]
    [InlineData("DWG read: Entry not found Metric50|725 for dictionary ACAD_DETAILVIEWSTYLE|602")]
    [InlineData("DXF bridge write: UnknownNonGraphicalObject not supported: ACDBDETAILVIEWSTYLE")]
    [InlineData("DWG read: Section not implemented THUMBNAILIMAGE")]
    public void OpaqueNonGraphicalMetadataNotificationsDoNotBlockOpen(string message)
    {
        Assert.True(CadAcadInteropDiagnostics.IsOpaqueMetadataNotification(message));
    }

    [Theory]
    [InlineData("DWG read: Unlisted entity with DXF name AEC_WALL has been read as an UnknownEntity")]
    [InlineData("DWG read: Entity not implemented WIPEOUT")]
    [InlineData("DWG read: Failed to resolve block reference 2A1")]
    [InlineData("DWG native semantic repair: dimension type DimensionOrdinate is recognized but has no matching UCAD 2D dimension entity yet.")]
    [InlineData("DWG paper layout import: non-rectangular viewport clipping was downgraded to rectangular bounds.")]
    public void GraphicalOrSemanticDiagnosticsRemainActionable(string message)
    {
        Assert.False(CadAcadInteropDiagnostics.IsOpaqueMetadataNotification(message));
    }

    [Fact]
    public void FilteringRemovesOnlyOpaqueMetadataAndKeepsActionableWarnings()
    {
        var metadata = "DWG read: Entry not found TCH_DBCONFIG|563 for dictionary |12";
        var graphical = "DWG read: Entity not implemented WIPEOUT";
        var semantic = "DWG native semantic repair: source leader annotation was not recoverable.";

        var filtered = CadAcadInteropDiagnostics.KeepActionableWarnings([metadata, graphical, semantic, graphical]);

        Assert.DoesNotContain(metadata, filtered);
        Assert.Contains(graphical, filtered);
        Assert.Contains(semantic, filtered);
        Assert.Equal(2, filtered.Count);
    }
}
