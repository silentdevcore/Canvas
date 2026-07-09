using PXA.Core.Abstractions;
using PXA.Core.Contracts;

namespace PXA.Core.Tests;

public sealed class AbstractionAdaptersTests
{
    [Fact]
    public void ExporterCapabilities_DefaultsMatchCanvasDefaults()
    {
        var pxaCapabilities = new ExporterCapabilities();
        var canvasCapabilities = new Canvas.Core.Abstractions.ExporterCapabilities();

        Assert.Equal(canvasCapabilities.SupportsMultiPage, pxaCapabilities.SupportsMultiPage);
        Assert.Equal(canvasCapabilities.SupportsImages, pxaCapabilities.SupportsImages);
        Assert.Equal(canvasCapabilities.SupportsRichText, pxaCapabilities.SupportsRichText);
        Assert.Equal(canvasCapabilities.SupportsFormFields, pxaCapabilities.SupportsFormFields);
    }

    [Fact]
    public void ExporterCapabilities_MapsToAndFromCanvasCapabilities()
    {
        var pxaCapabilities = new ExporterCapabilities(
            SupportsMultiPage: false,
            SupportsImages: true,
            SupportsRichText: false,
            SupportsFormFields: true);

        var canvasCapabilities = pxaCapabilities.ToCanvas();
        var roundTrip = canvasCapabilities.ToPxa();

        Assert.False(canvasCapabilities.SupportsMultiPage);
        Assert.True(canvasCapabilities.SupportsImages);
        Assert.False(roundTrip.SupportsRichText);
        Assert.True(roundTrip.SupportsFormFields);
    }

    [Fact]
    public void SimpleIoAbstractions_CanBeImplementedWithPxaNamespaces()
    {
        IDiagnosticsReader diagnosticsReader = new TestDiagnosticsReader();
        IOutputWriter outputWriter = new TestOutputWriter();
        IImageReader imageReader = new TestImageReader();

        Assert.Equal("diagnostics", diagnosticsReader.Read(new object()));
        outputWriter.Write("out.pdf", [1, 2, 3]);
        Assert.Equal("out.pdf", ((TestOutputWriter)outputWriter).Path);
        Assert.Equal("image.png", imageReader.Read("image.png"));
    }

    private sealed class TestDiagnosticsReader : IDiagnosticsReader
    {
        public object? Read(object documentModel) => "diagnostics";
    }

    private sealed class TestOutputWriter : IOutputWriter
    {
        public string? Path { get; private set; }

        public void Write(string path, byte[] data)
        {
            Path = path;
            Assert.Equal([1, 2, 3], data);
        }
    }

    private sealed class TestImageReader : IImageReader
    {
        public object Read(string path) => path;
    }
}
