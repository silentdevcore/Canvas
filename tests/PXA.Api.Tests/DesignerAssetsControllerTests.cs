using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkiaSharp;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Storage;

namespace PXA.Api.Tests;

public sealed class DesignerAssetsControllerTests
{
    [Fact]
    public async Task Upload_download_and_delete_are_tenant_scoped()
    {
        await using var dbContext = CreateContext();
        var storage = new MemoryObjectStorage();
        var options = Options.Create(new PxaStorageOptions());
        var service = new PxaStoredObjectService(dbContext, storage, options);
        var organizationId = Guid.NewGuid();
        var controller = CreateController(service, options, Guid.NewGuid(), organizationId);

        using var image = CreatePng();
        var upload = await controller.Upload(
            new FormFile(image, 0, image.Length, "file", "logo.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream",
            },
            CancellationToken.None);
        var created = Assert.IsType<CreatedResult>(upload.Result);
        var asset = Assert.IsType<DesignerAssetResponse>(created.Value);
        Assert.Equal("image/png", asset.ContentType);
        Assert.Equal(12, asset.Width);
        Assert.Equal(8, asset.Height);
        Assert.DoesNotContain("logo.png", asset.ContentUrl);

        var download = Assert.IsType<FileStreamResult>(
            await controller.Download(asset.Id, CancellationToken.None));
        Assert.Equal("image/png", download.ContentType);
        await download.FileStream.DisposeAsync();

        var otherTenant = CreateController(service, options, Guid.NewGuid(), Guid.NewGuid());
        Assert.IsType<NotFoundResult>(
            await otherTenant.Download(asset.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(
            (await otherTenant.GetMetadata(asset.Id, CancellationToken.None)).Result);

        Assert.IsType<NoContentResult>(
            await controller.Delete(asset.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(
            await controller.Download(asset.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Upload_rejects_invalid_images_and_configured_size_overflow()
    {
        await using var dbContext = CreateContext();
        var options = Options.Create(new PxaStorageOptions { MaximumDesignerAssetBytes = 8 });
        var service = new PxaStoredObjectService(dbContext, new MemoryObjectStorage(), options);
        var controller = CreateController(service, options, Guid.NewGuid(), Guid.NewGuid());

        using var invalid = new MemoryStream("not-image"u8.ToArray());
        var tooLarge = await controller.Upload(
            new FormFile(invalid, 0, invalid.Length, "file", "fake.png"),
            CancellationToken.None);
        Assert.Equal(413, Assert.IsType<ObjectResult>(tooLarge.Result).StatusCode);

        options.Value.MaximumDesignerAssetBytes = 100;
        invalid.Position = 0;
        var unsupported = await controller.Upload(
            new FormFile(invalid, 0, invalid.Length, "file", "fake.png"),
            CancellationToken.None);
        Assert.Equal(415, Assert.IsType<ObjectResult>(unsupported.Result).StatusCode);
    }

    private static MemoryStream CreatePng()
    {
        using var bitmap = new SKBitmap(12, 8);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray());
    }

    private static PxaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DesignerAssetsController CreateController(
        PxaStoredObjectService service,
        IOptions<PxaStorageOptions> options,
        Guid userId,
        Guid organizationId) => new(
            new TestTenantContext(userId, organizationId),
            service,
            options)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private sealed record TestTenantContext(Guid? UserId, Guid? OrganizationId) : IPxaTenantContext;

    private sealed class MemoryObjectStorage : IPxaObjectStorage
    {
        private readonly Dictionary<string, byte[]> objects = [];

        public async Task PutAsync(string objectKey, Stream content, CancellationToken cancellationToken)
        {
            using var target = new MemoryStream();
            await content.CopyToAsync(target, cancellationToken);
            objects.Add(objectKey, target.ToArray());
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(objects[objectKey], writable: false));

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(objects.ContainsKey(objectKey));

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            objects.Remove(objectKey);
            return Task.CompletedTask;
        }
    }
}
