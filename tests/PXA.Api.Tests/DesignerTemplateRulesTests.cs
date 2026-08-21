using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class DesignerTemplateRulesTests
{
    [Fact]
    public async Task Draft_version_publish_archive_and_checksum_rules_are_consistent()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        var firstDesign = JsonSerializer.SerializeToElement(new { text = "First" });

        var created = Read<CreatedAtActionResult, DesignerTemplateDocument>(
            await controller.Create(
                new CreateDesignerTemplateRequest("Rules", null, null, firstDesign),
                CancellationToken.None));
        Assert.Equal(1, created.Revision);
        Assert.Equal(Checksum(created.DesignDocument), created.Checksum);
        Assert.Equal("Rules", created.DesignDocument.GetProperty("template").GetProperty("name").GetString());

        var noOp = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.UpdateDraft(
                created.Id,
                new UpdateDesignerTemplateDraftRequest(1, firstDesign),
                CancellationToken.None));
        Assert.Equal(1, noOp.Revision);

        var secondDesign = JsonSerializer.SerializeToElement(new { text = "Second" });
        var updated = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.UpdateDraft(
                created.Id,
                new UpdateDesignerTemplateDraftRequest(1, secondDesign),
                CancellationToken.None));
        Assert.Equal(2, updated.Revision);
        Assert.Equal(Checksum(updated.DesignDocument), updated.Checksum);

        var firstVersion = Read<CreatedAtActionResult, CreateDesignerTemplateVersionResponse>(
            await controller.CreateVersion(
                created.Id,
                new CreateDesignerTemplateVersionRequest(2, "Approved"),
                CancellationToken.None));
        Assert.True(firstVersion.Created);
        Assert.Equal(1, firstVersion.Version.VersionNumber);
        Assert.Equal(updated.Checksum, firstVersion.Version.Checksum);

        var duplicateVersion = Read<OkObjectResult, CreateDesignerTemplateVersionResponse>(
            await controller.CreateVersion(
                created.Id,
                new CreateDesignerTemplateVersionRequest(2, "Duplicate"),
                CancellationToken.None));
        Assert.False(duplicateVersion.Created);
        Assert.Equal(firstVersion.Version.Id, duplicateVersion.Version.Id);

        var published = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.Publish(
                created.Id,
                new PublishDesignerTemplateRequest(2, 1),
                CancellationToken.None));
        Assert.Equal(3, published.Revision);
        Assert.Equal(firstVersion.Version.Id, published.PublishedVersionId);

        var archived = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.Archive(
                created.Id,
                new TemplateRevisionRequest(3),
                CancellationToken.None));
        Assert.Equal(4, archived.Revision);
        Assert.Equal("Archived", archived.Status);

        var repeatedArchive = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.Archive(
                created.Id,
                new TemplateRevisionRequest(4),
                CancellationToken.None));
        Assert.Equal(4, repeatedArchive.Revision);

        var restored = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.Restore(
                created.Id,
                new TemplateRevisionRequest(4),
                CancellationToken.None));
        Assert.Equal(5, restored.Revision);
        Assert.Equal("Draft", restored.Status);

        var thirdDesign = JsonSerializer.SerializeToElement(new { text = "Third" });
        _ = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.UpdateDraft(
                created.Id,
                new UpdateDesignerTemplateDraftRequest(5, thirdDesign),
                CancellationToken.None));
        var immutableVersion = Read<OkObjectResult, DesignerTemplateVersionDocument>(
            await controller.GetVersion(created.Id, 1, CancellationToken.None));
        Assert.Equal(updated.DesignDocument.GetRawText(), immutableVersion.DesignDocument.GetRawText());
        Assert.NotEqual(thirdDesign.GetRawText(), immutableVersion.DesignDocument.GetRawText());
    }

    [Fact]
    public async Task Draft_and_metadata_updates_keep_the_authoritative_name_in_sync()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        var created = Read<CreatedAtActionResult, DesignerTemplateDocument>(
            await controller.Create(
                new CreateDesignerTemplateRequest("Original", "Description", ["tag"],
                    JsonSerializer.SerializeToElement(new
                    {
                        template = new { name = "Stale client name" },
                        pages = Array.Empty<object>(),
                    })),
                CancellationToken.None));

        Assert.Equal("Original", created.Name);
        Assert.Equal("Original", created.DesignDocument.GetProperty("template").GetProperty("name").GetString());

        var renamedByDraft = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.UpdateDraft(created.Id,
                new UpdateDesignerTemplateDraftRequest(created.Revision,
                    JsonSerializer.SerializeToElement(new
                    {
                        template = new { name = "Quarterly Report" },
                        pages = Array.Empty<object>(),
                    })),
                CancellationToken.None));
        Assert.Equal("Quarterly Report", renamedByDraft.Name);
        Assert.Equal("Quarterly Report", renamedByDraft.DesignDocument.GetProperty("template").GetProperty("name").GetString());

        var renamedByMetadata = Read<OkObjectResult, DesignerTemplateDocument>(
            await controller.UpdateMetadata(created.Id,
                new UpdateDesignerTemplateMetadataRequest(renamedByDraft.Revision, "Annual Report", "Description", ["tag"]),
                CancellationToken.None));
        Assert.Equal("Annual Report", renamedByMetadata.Name);
        Assert.Equal("Annual Report", renamedByMetadata.DesignDocument.GetProperty("template").GetProperty("name").GetString());

        var invalid = await controller.UpdateDraft(created.Id,
            new UpdateDesignerTemplateDraftRequest(renamedByMetadata.Revision,
                JsonSerializer.SerializeToElement(new { template = new { name = "   " } })),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(invalid.Result);
    }

    [Fact]
    public async Task Design_json_accepts_exactly_ten_mib_and_rejects_invalid_boundaries()
    {
        const int limit = 10 * 1024 * 1024;
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        var exactLimit = JsonSerializer.SerializeToElement(new string('x', limit - 2));
        Assert.Equal(limit, Encoding.UTF8.GetByteCount(exactLimit.GetRawText()));

        var accepted = await controller.Create(
            new CreateDesignerTemplateRequest("Exact limit", null, null, exactLimit),
            CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(accepted.Result);

        var overLimit = JsonSerializer.SerializeToElement(new string('x', limit - 1));
        var rejected = await controller.Create(
            new CreateDesignerTemplateRequest("Over limit", null, null, overLimit),
            CancellationToken.None);
        var oversized = Assert.IsType<ObjectResult>(rejected.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, oversized.StatusCode);

        var missing = await controller.Create(
            new CreateDesignerTemplateRequest("Missing", null, null, default),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(missing.Result);

        using var nullJson = JsonDocument.Parse("null");
        var explicitNull = await controller.Create(
            new CreateDesignerTemplateRequest(
                "Null",
                null,
                null,
                nullJson.RootElement.Clone()),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(explicitNull.Result);
    }

    private static PxaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PxaDbContext(options);
    }

    private static DesignerTemplatesController CreateController(PxaDbContext dbContext)
    {
        var controller = new DesignerTemplatesController(
            dbContext,
            new TestTenantContext(Guid.NewGuid(), Guid.NewGuid()),
            Options.Create(new PxaDesignerTemplateOptions()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    private static TValue Read<TResult, TValue>(ActionResult<TValue> action)
        where TResult : ObjectResult
    {
        var result = Assert.IsType<TResult>(action.Result);
        return Assert.IsType<TValue>(result.Value);
    }

    private static string Checksum(JsonElement value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.GetRawText())));

    private sealed record TestTenantContext(Guid? UserId, Guid? OrganizationId) : IPxaTenantContext;
}
