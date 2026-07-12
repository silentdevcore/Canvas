using PXA.Pdf;
using PXA.Infrastructure.Pdf;

namespace PXA.Infrastructure.Pdf.Tests;

public sealed class PdfInfrastructureFacadeTests
{
    [Fact]
    public void Renderer_RendersPdfDocumentToBytes()
    {
        var document = BuildDocument();

        var bytes = new PdfDocumentRenderer().Render(document);

        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void Facade_GeneratesPdfFile()
    {
        var document = BuildDocument();
        var path = Path.Combine(Path.GetTempPath(), $"pxa-pdf-{Guid.NewGuid():N}.pdf");

        try
        {
            new PdfFacade().GenerateToFile(document, path);

            Assert.True(File.Exists(path));
            Assert.Equal("%PDF"u8.ToArray(), File.ReadAllBytes(path)[..4]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void DiagnosticsReader_ReturnsLastDiagnosticsAfterSave()
    {
        var document = BuildDocument();
        _ = document.ToBytes(new PdfSaveOptions { CollectDiagnostics = true });

        var diagnostics = new PdfDiagnosticsReader().Read(document);

        Assert.NotNull(diagnostics);
    }

    [Fact]
    public void Capabilities_MirrorPxaPdfRendererCapabilities()
    {
        var capabilities = new PdfRendererCapabilities();

        Assert.Equal("pdf", capabilities.RendererKey);
        Assert.True(capabilities.SupportsBookmarks);
        Assert.True(capabilities.SupportsCompression);
        Assert.True(capabilities.SupportsWatermarks);
    }

    [Fact]
    public void Services_DelegateToPxaPdfDocument()
    {
        var document = BuildDocument();

        new PdfPageNumberingService().Apply(document);
        new PdfHeaderFooterService().Apply(document);
        new PdfWatermarkService().Apply(document, "PXA");
        var bytes = new PdfDocumentRenderer().Render(document);

        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    private static PdfDocument BuildDocument()
    {
        var document = new PdfDocument();
        var page = document.AddPage(300, 180);
        page.DrawTextFromTop("PXA infrastructure", 24, 24, 12);
        return document;
    }
}
