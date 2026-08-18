using UCAD.Core.IO;
using UCAD.Core.Layout;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class LegacyReaderForwardingTests
{
    [Fact]
    public void V11ReaderForwardsLayoutAwarePayloadToCurrentLayoutReader()
    {
        var document = new CadDocument();
        var setup = new CadPageSetup(CadPaperSize.A1, landscape: false, plotScaleDenominator: 750);
        document.SetLayoutTable([new CadLayoutDefinition("Presentation", setup)], "Presentation");

        var json = CadNativeDocumentCodecLayout.Serialize(document);
        var restored = CadNativeDocumentCodecV11.Deserialize(json);

        Assert.Equal("Presentation", restored.ActiveLayoutName);
        Assert.Equal(CadPaperSize.A1, restored.ActivePageSetup.PaperSize);
        Assert.Equal(750, restored.ActivePageSetup.PlotScaleDenominator);
    }
}
