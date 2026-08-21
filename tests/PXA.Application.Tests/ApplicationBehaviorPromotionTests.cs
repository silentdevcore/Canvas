using PXA.Application.Queries;
using PXA.Application.UseCases;
using PXA.Core.Abstractions;
using PXA.Core.Contracts;

namespace PXA.Application.Tests;

public sealed class ApplicationBehaviorPromotionTests
{
    [Fact]
    public void ExportDocument_UsesPxaDocumentExporter()
    {
        var exporter = new TestExporter();
        var useCase = new ExportDocumentUseCase([exporter]);

        var result = useCase.Execute(new ExportDocumentRequest(
            new DesignExportDto { Id = "design-1", Name = "Invoice:July" },
            "test"));

        Assert.Equal([1, 2, 3], result.Data);
        Assert.Equal("application/test", result.MimeType);
        Assert.Equal("Invoice-July.test", result.FileName);
        Assert.Equal("design-1", exporter.LastDesign?.Id);
    }

    [Fact]
    public void GenerateDocument_RendersAndWritesOutput()
    {
        var renderer = new TestRenderer();
        var writer = new TestOutputWriter();
        var useCase = new GenerateDocumentUseCase(renderer, writer);
        var document = new object();

        useCase.Execute(new GenerateDocumentRequest(document, "out.pdf"));

        Assert.Same(document, renderer.LastDocumentModel);
        Assert.Equal("out.pdf", writer.Path);
        Assert.Equal([9, 8, 7], writer.Data);
    }

    [Fact]
    public void ApplyWatermark_ValidatesTextAndDelegatesToService()
    {
        var service = new TestWatermarkService();
        var useCase = new ApplyWatermarkUseCase(service);
        var document = new object();

        useCase.Execute(document, "Draft", new { Opacity = 0.5 });

        Assert.Same(document, service.DocumentModel);
        Assert.Equal("Draft", service.Text);
        Assert.NotNull(service.Options);
        Assert.Throws<ArgumentException>(() => useCase.Execute(document, ""));
    }

    [Fact]
    public void PageCoverageQueryService_DelegatesAllCoverageQueries()
    {
        var inner = new TestPageCoverageService();
        var service = new PageCoverageQueryService(inner);
        var document = new object();

        Assert.Equal([1], service.GetPagesWithText(document));
        Assert.Equal([2], service.GetPagesWithImages(document));
        Assert.Equal([3], service.GetPagesWithLinks(document));
        Assert.Equal([4], service.GetPagesWithShapes(document));
        Assert.Equal(4, inner.CallCount);
    }

    private sealed class TestExporter : IDocumentExporter
    {
        public string FormatKey => "test";
        public string MimeType => "application/test";
        public string FileExtension => ".test";
        public DesignExportDto? LastDesign { get; private set; }

        public byte[] Export(DesignExportDto design)
        {
            LastDesign = design;
            return [1, 2, 3];
        }
    }

    private sealed class TestRenderer : IDocumentRenderer
    {
        public object? LastDocumentModel { get; private set; }

        public byte[] Render(object documentModel)
        {
            LastDocumentModel = documentModel;
            return [9, 8, 7];
        }
    }

    private sealed class TestOutputWriter : IOutputWriter
    {
        public string? Path { get; private set; }
        public byte[]? Data { get; private set; }

        public void Write(string path, byte[] data)
        {
            Path = path;
            Data = data;
        }
    }

    private sealed class TestWatermarkService : IWatermarkService
    {
        public object? DocumentModel { get; private set; }
        public string? Text { get; private set; }
        public object? Options { get; private set; }

        public void Apply(object documentModel, string text, object? options = null)
        {
            DocumentModel = documentModel;
            Text = text;
            Options = options;
        }
    }

    private sealed class TestPageCoverageService : IPageCoverageQueryService
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<int> GetPagesWithText(object documentModel)
        {
            CallCount++;
            return [1];
        }

        public IReadOnlyList<int> GetPagesWithImages(object documentModel)
        {
            CallCount++;
            return [2];
        }

        public IReadOnlyList<int> GetPagesWithLinks(object documentModel)
        {
            CallCount++;
            return [3];
        }

        public IReadOnlyList<int> GetPagesWithShapes(object documentModel)
        {
            CallCount++;
            return [4];
        }
    }
}
