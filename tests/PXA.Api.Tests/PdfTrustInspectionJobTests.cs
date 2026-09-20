using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Importer.Trust;
using PXA.Pdf;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Jobs;
using PXA.WebApi.Services.Pdf;
using PXA.WebApi.Services.Storage;

namespace PXA.Api.Tests;

public sealed class PdfTrustInspectionJobTests
{
    [Fact]
    public async Task Executor_stores_tenant_scoped_report_and_correlates_immutable_audit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pxa-trust-job-{Guid.NewGuid():N}");
        try
        {
            await using var context = CreateContext();
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var storedObjects = CreateStoredObjects(context, root);
            var document = new PdfDocument();
            document.AddPage().DrawText("Tenant trust job", 36, 36);
            await using var source = new MemoryStream(document.ToBytes(), writable: false);
            var input = await storedObjects.StoreAsync(
                organizationId,
                userId,
                "job-input",
                "application/pdf",
                "source.pdf",
                source,
                CancellationToken.None);
            var job = new PxaBackgroundJob
            {
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PxaJobQueue.PdfTrustInspectionType,
                InputObjectId = input.Id,
                PayloadJson = JsonSerializer.Serialize(
                    new PdfTrustInspectionJobPayload("configured-validation-policy")),
            };
            context.BackgroundJobs.Add(job);
            await context.SaveChangesAsync();
            var auditWriter = new PxaPdfTrustAuditWriter(
                context,
                new TestTenantContext(userId, organizationId),
                new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
            var executor = new PdfTrustInspectionJobExecutor(
                storedObjects,
                new PdfTrustInspector(),
                new TestTrustPolicyProvider(),
                auditWriter);

            var execution = await executor.ExecuteAsync(job, CancellationToken.None);
            var retryExecution = await executor.ExecuteAsync(job, CancellationToken.None);

            var result = await storedObjects.OpenAsync(execution.Result.Id, organizationId, CancellationToken.None);
            Assert.NotNull(result);
            await using (result.Value.Content)
            {
                using var json = await JsonDocument.ParseAsync(result.Value.Content);
                Assert.True(json.RootElement.TryGetProperty("signatures", out var signatures));
                Assert.Equal(JsonValueKind.Array, signatures.ValueKind);
                Assert.False(json.RootElement.TryGetProperty("Signatures", out _));
                var report = json.RootElement.Deserialize<PdfTrustReport>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Assert.NotNull(report);
                Assert.Equal(input.Checksum, report.Sha256);
            }
            Assert.Null(await storedObjects.OpenAsync(
                execution.Result.Id,
                Guid.NewGuid(),
                CancellationToken.None));
            var operation = Assert.Single(context.PdfTrustOperationJobs);
            var audit = Assert.Single(context.AuditEvents);
            Assert.Equal(job.Id, operation.BackgroundJobId);
            Assert.Equal(audit.Id, operation.AuditEventId);
            Assert.Equal(organizationId, operation.OrganizationId);
            Assert.Equal("application/json", execution.Result.ContentType);
            Assert.Equal(execution.Result.Id, retryExecution.Result.Id);
            Assert.Single(context.StoredObjects.Where(value => value.Purpose == "job-result"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Executor_honors_cancellation_before_reading_tenant_input()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pxa-trust-job-cancel-{Guid.NewGuid():N}");
        try
        {
            await using var context = CreateContext();
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var storedObjects = CreateStoredObjects(context, root);
            await using var source = new MemoryStream("%PDF-1.7\n%%EOF\n"u8.ToArray(), writable: false);
            var input = await storedObjects.StoreAsync(
                organizationId, userId, "job-input", "application/pdf", "source.pdf", source, CancellationToken.None);
            var job = new PxaBackgroundJob
            {
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PxaJobQueue.PdfTrustInspectionType,
                InputObjectId = input.Id,
                PayloadJson = "{}",
            };
            var executor = new PdfTrustInspectionJobExecutor(
                storedObjects,
                new PdfTrustInspector(),
                new TestTrustPolicyProvider(),
                new PxaPdfTrustAuditWriter(
                    context,
                    new TestTenantContext(userId, organizationId),
                    new HttpContextAccessor { HttpContext = new DefaultHttpContext() }));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => executor.ExecuteAsync(job, cancellation.Token));
            Assert.Empty(context.PdfTrustOperationJobs);
            Assert.Empty(context.AuditEvents);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Executor_removes_result_when_immutable_audit_cannot_be_written()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pxa-trust-job-audit-{Guid.NewGuid():N}");
        try
        {
            await using var context = CreateContext();
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var storedObjects = CreateStoredObjects(context, root);
            var document = new PdfDocument();
            document.AddPage().DrawText("Audit failure", 36, 36);
            await using var source = new MemoryStream(document.ToBytes(), writable: false);
            var input = await storedObjects.StoreAsync(
                organizationId, userId, "job-input", "application/pdf", "source.pdf", source, CancellationToken.None);
            var job = new PxaBackgroundJob
            {
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PxaJobQueue.PdfTrustInspectionType,
                InputObjectId = input.Id,
                PayloadJson = "{}",
            };
            var executor = new PdfTrustInspectionJobExecutor(
                storedObjects,
                new PdfTrustInspector(),
                new TestTrustPolicyProvider(),
                new FailingAuditWriter());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => executor.ExecuteAsync(job, CancellationToken.None));

            var result = Assert.Single(context.StoredObjects.Where(value => value.Purpose == "job-result"));
            Assert.Equal(PxaStoredObjectStatus.Deleted, result.Status);
            Assert.Null(job.ResultObjectId);
            Assert.Null(job.DiagnosticsJson);
            Assert.Null(await storedObjects.OpenAsync(result.Id, organizationId, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static PxaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase($"pdf-trust-job-{Guid.NewGuid():N}")
            .Options);

    private static PxaStoredObjectService CreateStoredObjects(PxaDbContext context, string root)
    {
        var options = Options.Create(new PxaStorageOptions { RootPath = root });
        var storage = new FileSystemPxaObjectStorage(
            options,
            new TestWebHostEnvironment { ContentRootPath = root });
        return new PxaStoredObjectService(context, storage, options);
    }

    private sealed class TestTrustPolicyProvider : IPxaPdfTrustPolicyProvider
    {
        public PdfSignatureValidationPolicy Policy { get; } = new();
    }

    private sealed class FailingAuditWriter : IPxaPdfTrustAuditWriter
    {
        public Task WriteAsync(PxaPdfTrustAuditEntry entry, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The audit store is unavailable.");
    }

    private sealed record TestTenantContext(Guid CurrentUserId, Guid CurrentOrganizationId) : IPxaTenantContext
    {
        public Guid? UserId => CurrentUserId;
        public Guid? OrganizationId => CurrentOrganizationId;
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
