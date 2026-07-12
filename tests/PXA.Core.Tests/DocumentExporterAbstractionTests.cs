using PXA.Core.Abstractions;
using PXA.Core.Contracts;

namespace PXA.Core.Tests;

public sealed class DocumentExporterAbstractionTests
{
    [Fact]
    public void DocumentExporter_DefaultCapabilitiesMatchPxaDefaults()
    {
        IDocumentExporter exporter = new TestDocumentExporter();
        var canvasCapabilities = new ExporterCapabilities();

        Assert.Equal("test", exporter.FormatKey);
        Assert.Equal("application/test", exporter.MimeType);
        Assert.Equal(".test", exporter.FileExtension);
        Assert.Equal(canvasCapabilities.SupportsMultiPage, exporter.Capabilities.SupportsMultiPage);
        Assert.Equal(canvasCapabilities.SupportsImages, exporter.Capabilities.SupportsImages);
        Assert.Equal(canvasCapabilities.SupportsRichText, exporter.Capabilities.SupportsRichText);
        Assert.Equal(canvasCapabilities.SupportsFormFields, exporter.Capabilities.SupportsFormFields);
    }

    [Fact]
    public void DocumentExporter_OptionsOverloadFallsBackToDesignExport()
    {
        IDocumentExporter exporter = new TestDocumentExporter();
        var design = new DesignExportDto { Id = "design-1", Name = "Design 1" };
        var output = exporter.Export(design, new ExportOptions(Dpi: 144, Quality: 90));
        var concreteExporter = (TestDocumentExporter)exporter;

        Assert.Equal([4, 2], output);
        Assert.Same(design, concreteExporter.LastDesign);
        Assert.Equal(1, concreteExporter.ExportCount);
    }

    private sealed class TestDocumentExporter : IDocumentExporter
    {
        public string FormatKey => "test";
        public string MimeType => "application/test";
        public string FileExtension => ".test";
        public DesignExportDto? LastDesign { get; private set; }
        public int ExportCount { get; private set; }

        public byte[] Export(DesignExportDto design)
        {
            LastDesign = design;
            ExportCount++;
            return [4, 2];
        }
    }
}
