using Canvas.Application.UseCases;
using Canvas.Core.Abstractions;

namespace Canvas.Application.Tests;

public class GenerateDocumentUseCaseTests
{
    [Fact]
    public void Execute_ShouldRenderAndWrite_WhenRequestIsValid()
    {
        var renderer = new FakeRenderer();
        var writer = new FakeOutputWriter();
        var sut = new GenerateDocumentUseCase(renderer, writer);
        var model = new object();

        sut.Execute(new GenerateDocumentRequest(model, "out.pdf"));

        Assert.Same(model, renderer.ReceivedModel);
        Assert.Equal("out.pdf", writer.Path);
        Assert.Equal(new byte[] { 1, 2, 3 }, writer.Data);
    }

    [Fact]
    public void Execute_ShouldThrow_WhenOutputPathIsBlank()
    {
        var sut = new GenerateDocumentUseCase(new FakeRenderer(), new FakeOutputWriter());

        Assert.Throws<ArgumentException>(() => sut.Execute(new GenerateDocumentRequest(new object(), " ")));
    }
}

public class ApplyWatermarkUseCaseTests
{
    [Fact]
    public void Execute_ShouldThrow_WhenTextIsBlank()
    {
        var sut = new ApplyWatermarkUseCase(new FakeWatermarkService());

        Assert.Throws<ArgumentException>(() => sut.Execute(new object(), ""));
    }

    [Fact]
    public void Execute_ShouldDelegate_WhenInputIsValid()
    {
        var service = new FakeWatermarkService();
        var sut = new ApplyWatermarkUseCase(service);
        var model = new object();

        sut.Execute(model, "draft");

        Assert.Same(model, service.DocumentModel);
        Assert.Equal("draft", service.Text);
    }
}

public class CollectDiagnosticsUseCaseTests
{
    [Fact]
    public void Execute_ShouldReturnReaderResult()
    {
        var expected = new { Name = "diag" };
        var reader = new FakeDiagnosticsReader { ReturnValue = expected };
        var sut = new CollectDiagnosticsUseCase(reader);

        var result = sut.Execute(new object());

        Assert.Same(expected, result);
    }
}

file sealed class FakeRenderer : IDocumentRenderer
{
    public object? ReceivedModel { get; private set; }

    public byte[] Render(object documentModel)
    {
        ReceivedModel = documentModel;
        return new byte[] { 1, 2, 3 };
    }
}

file sealed class FakeOutputWriter : IOutputWriter
{
    public string? Path { get; private set; }

    public byte[]? Data { get; private set; }

    public void Write(string path, byte[] data)
    {
        Path = path;
        Data = data;
    }
}

file sealed class FakeWatermarkService : IWatermarkService
{
    public object? DocumentModel { get; private set; }

    public string? Text { get; private set; }

    public void Apply(object documentModel, string text, object? options = null)
    {
        DocumentModel = documentModel;
        Text = text;
    }
}

file sealed class FakeDiagnosticsReader : IDiagnosticsReader
{
    public object? ReturnValue { get; init; }

    public object? Read(object documentModel)
    {
        return ReturnValue;
    }
}
