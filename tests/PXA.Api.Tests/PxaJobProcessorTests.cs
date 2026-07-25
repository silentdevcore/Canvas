using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Services.Jobs;
using PXA.WebApi.Services.Storage;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class PxaJobProcessorTests
{
    [PostgreSqlFact]
    public async Task Processes_a_tenant_template_render_into_external_result_storage()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var dbOptions = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), $"pxa-job-storage-{Guid.NewGuid():N}");

        try
        {
            await using var context = new PxaDbContext(dbOptions);
            await context.Database.MigrateAsync();
            context.Users.Add(new PxaIdentityUser
            {
                Id = userId,
                UserName = "job-processor",
                NormalizedUserName = "JOB-PROCESSOR",
                Email = "job-processor@pxa.local",
                NormalizedEmail = "JOB-PROCESSOR@PXA.LOCAL",
                DisplayName = "Job Processor",
            });
            context.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Job Processor Organization",
                Slug = "job-processor",
            });
            context.DesignerTemplates.Add(new DesignerTemplate
            {
                Id = templateId,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                Name = "Async Invoice",
                DraftJson = """{"pages":[]}""",
                DraftChecksum = new string('a', 64),
                SchemaVersion = "1.0",
                DesignerVersion = "test",
            });
            context.BackgroundJobs.Add(new PxaBackgroundJob
            {
                Id = jobId,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PxaJobQueue.TemplateRenderType,
                PayloadJson = JsonSerializer.Serialize(new TemplateRenderJobPayload(
                    templateId.ToString(),
                    JsonSerializer.SerializeToElement(new { invoiceNumber = "PXA-1" }),
                    null)),
            });
            await context.SaveChangesAsync();

            var storageOptions = Options.Create(new PxaStorageOptions { RootPath = root });
            var storage = new FileSystemPxaObjectStorage(
                storageOptions,
                new TestWebHostEnvironment { ContentRootPath = root });
            var storedObjects = new PxaStoredObjectService(context, storage, storageOptions);
            var processor = new PxaJobProcessor(
                context,
                storedObjects,
                [],
                new PXA.Application.UseCases.ExportDocumentUseCase(
                    Array.Empty<PXA.Core.Abstractions.IDocumentExporter>()),
                new PXA.WebApi.Services.MigrationService(),
                Options.Create(new PxaJobOptions()),
                NullLogger<PxaJobProcessor>.Instance);

            Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

            context.ChangeTracker.Clear();
            var completed = await context.BackgroundJobs.SingleAsync(value => value.Id == jobId);
            Assert.Equal(PxaBackgroundJobStatus.Completed, completed.Status);
            Assert.NotNull(completed.ResultObjectId);
            var result = await storedObjects.OpenAsync(
                completed.ResultObjectId.Value,
                organizationId,
                CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("application/pdf", result.Value.Metadata.ContentType);
            Assert.True(result.Value.Metadata.Length > 0);
            await result.Value.Content.DisposeAsync();

            completed.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
            var retention = new PxaJobRetentionService(
                context,
                storedObjects,
                Options.Create(new PxaJobOptions { CleanupBatchSize = 10 }));

            Assert.Equal(1, await retention.CleanupAsync(CancellationToken.None));
            context.ChangeTracker.Clear();
            var expired = await context.BackgroundJobs.SingleAsync(value => value.Id == jobId);
            var deletedResult = await context.StoredObjects.SingleAsync();
            Assert.Equal(PxaBackgroundJobStatus.Expired, expired.Status);
            Assert.Null(expired.ResultObjectId);
            Assert.Equal(PxaStoredObjectStatus.Deleted, deletedResult.Status);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

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
