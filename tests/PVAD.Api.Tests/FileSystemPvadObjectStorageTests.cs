using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Services.Storage;

namespace PVAD.Api.Tests;

public sealed class FileSystemPvadObjectStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"pvad-storage-{Guid.NewGuid():N}");

    [Fact]
    public async Task Explicit_shared_mount_preserves_keys_and_bytes_across_renamed_application_roots()
    {
        var mount = Path.Combine(root, "retained-pxa-mount");
        const string key = "synthetic-tenant/pxa-retained-object";
        var bytes = "Synthetic PXA customer content must remain unchanged."u8.ToArray();
        var before = new FileSystemPvadObjectStorage(
            Options.Create(new PvadStorageOptions { RootPath = mount }),
            new TestWebHostEnvironment { ContentRootPath = Path.Combine(root, "PXA.WebApi") });
        await using (var source = new MemoryStream(bytes))
            await before.PutAsync(key, source, CancellationToken.None);

        var after = new FileSystemPvadObjectStorage(
            Options.Create(new PvadStorageOptions { RootPath = mount }),
            new TestWebHostEnvironment { ContentRootPath = Path.Combine(root, "PVAD.WebApi") });
        Assert.True(await after.ExistsAsync(key, CancellationToken.None));
        await using (var content = await after.OpenReadAsync(key, CancellationToken.None))
        {
            using var result = new MemoryStream();
            await content.CopyToAsync(result);
            Assert.Equal(bytes, result.ToArray());
        }
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(mount, "synthetic-tenant", "pxa-retained-object")));
        await using var duplicate = new MemoryStream("replacement"u8.ToArray());
        await Assert.ThrowsAsync<IOException>(() => after.PutAsync(key, duplicate, CancellationToken.None));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(mount, "synthetic-tenant", "pxa-retained-object")));
        Assert.Empty(Directory.GetFiles(mount, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Relative_default_does_not_silently_find_an_old_root_but_explicit_mount_restores_access()
    {
        var originalRoot = Path.Combine(root, "PXA.WebApi");
        var renamedRoot = Path.Combine(root, "PVAD.WebApi");
        var before = new FileSystemPvadObjectStorage(Options.Create(new PvadStorageOptions()),
            new TestWebHostEnvironment { ContentRootPath = originalRoot });
        await using (var source = new MemoryStream("retained synthetic bytes"u8.ToArray()))
            await before.PutAsync("tenant/object", source, CancellationToken.None);
        var defaultAfter = new FileSystemPvadObjectStorage(Options.Create(new PvadStorageOptions()),
            new TestWebHostEnvironment { ContentRootPath = renamedRoot });
        Assert.False(await defaultAfter.ExistsAsync("tenant/object", CancellationToken.None));

        var configuredAfter = new FileSystemPvadObjectStorage(
            Options.Create(new PvadStorageOptions { RootPath = Path.Combine(originalRoot, "App_Data", "objects") }),
            new TestWebHostEnvironment { ContentRootPath = renamedRoot });
        Assert.True(await configuredAfter.ExistsAsync("tenant/object", CancellationToken.None));
    }

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

    private FileSystemPvadObjectStorage CreateStorage() => new(
        Options.Create(new PvadStorageOptions { RootPath = root }),
        new TestWebHostEnvironment { ContentRootPath = root });

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PVAD.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
