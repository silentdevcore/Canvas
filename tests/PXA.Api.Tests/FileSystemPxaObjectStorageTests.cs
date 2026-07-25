using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PXA.WebApi.Services.Storage;

namespace PXA.Api.Tests;

public sealed class FileSystemPxaObjectStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"pxa-storage-{Guid.NewGuid():N}");

    [Fact]
    public async Task Writes_reads_and_deletes_an_object_atomically()
    {
        var storage = CreateStorage();
        await using var source = new MemoryStream("stored content"u8.ToArray());

        await storage.PutAsync("tenant/object", source, CancellationToken.None);
        await using var result = await storage.OpenReadAsync("tenant/object", CancellationToken.None);
        using var reader = new StreamReader(result);

        Assert.Equal("stored content", await reader.ReadToEndAsync());
        await storage.DeleteAsync("tenant/object", CancellationToken.None);
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => storage.OpenReadAsync("tenant/object", CancellationToken.None));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("tenant/../../secret")]
    [InlineData("/absolute")]
    [InlineData("tenant\\secret")]
    [InlineData("tenant/file.txt")]
    public async Task Rejects_keys_that_are_not_generated_tenant_paths(string objectKey)
    {
        var storage = CreateStorage();
        await using var source = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.PutAsync(objectKey, source, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private FileSystemPxaObjectStorage CreateStorage() => new(
        Options.Create(new PxaStorageOptions { RootPath = root }),
        new TestWebHostEnvironment { ContentRootPath = root });

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PXA.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
