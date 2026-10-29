using System.Collections.Concurrent;
using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Observability;
using PVAD.WebApi.Services.Jobs;
using PVAD.WebApi.Services.Licensing;
using PVAD.WebApi.Services.Storage;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class PvadJobProcessorTests
{
    [Fact]
    public async Task Queue_defaults_to_transient_and_requires_explicit_retained_mode()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inputId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PvadDbContext>()
            .UseInMemoryDatabase($"job-retention-{Guid.NewGuid():N}")
            .Options;
        await using var context = new PvadDbContext(options);
        context.StoredObjects.Add(new PvadStoredObject
        {
            Id = inputId,
            OrganizationId = organizationId,
            CreatedByUserId = userId,
            ObjectKey = "test/input",
            Purpose = "job-input",
            ContentType = "application/json",
            Length = 2,
            Checksum = new string('a', 64),
        });
        await context.SaveChangesAsync();
        var settings = Options.Create(new PvadJobOptions
        {
            TransientRetentionHours = 24,
            PdfTrustInspectionRetentionHours = 1,
            ResultRetentionDays = 7,
            TerminalMetadataRetentionDays = 30,
        });
        var queue = new PvadJobQueue(
            context,
            new TestTenantContext(userId, organizationId),
            settings);
        var before = DateTimeOffset.UtcNow;

        var transient = await queue.EnqueueDocumentJobAsync(
            PvadJobQueue.DocumentExportType,
            inputId,
            new DocumentExportJobPayload("pdf", null, null),
            CancellationToken.None);
        var retained = await queue.EnqueueDocumentJobAsync(
            PvadJobQueue.DocumentExportType,
            inputId,
            new DocumentExportJobPayload("pdf", null, null),
            CancellationToken.None,
            PvadJobRetentionMode.Retained);
        var trustInspection = await queue.EnqueueDocumentJobAsync(
            PvadJobQueue.PdfTrustInspectionType,
            inputId,
            new PdfTrustInspectionJobPayload("configured-validation-policy"),
            CancellationToken.None);

        Assert.Equal(PvadJobRetentionMode.Transient, transient.RetentionMode);
        Assert.InRange(transient.ExpiresAt, before.AddHours(24), DateTimeOffset.UtcNow.AddHours(24));
        Assert.Equal(PvadJobRetentionMode.Retained, retained.RetentionMode);
        Assert.InRange(retained.ExpiresAt, before.AddDays(7), DateTimeOffset.UtcNow.AddDays(7));
        Assert.InRange(transient.MetadataExpiresAt, before.AddDays(30), DateTimeOffset.UtcNow.AddDays(30));
        Assert.Equal(PvadJobQueue.PdfTrustInspectionType, trustInspection.Type);
        Assert.Equal(PvadJobRetentionMode.Transient, trustInspection.RetentionMode);
        Assert.InRange(
            trustInspection.ExpiresAt,
            before.AddHours(1),
            DateTimeOffset.UtcNow.AddHours(1));
    }

    [PostgreSqlFact]
    public async Task Processes_a_tenant_template_render_into_external_result_storage()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var dbOptions = new DbContextOptionsBuilder<PvadDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var traceParent = $"00-{ActivityTraceId.CreateRandom()}-{ActivitySpanId.CreateRandom()}-01";
        var root = Path.Combine(Path.GetTempPath(), $"pxa-job-storage-{Guid.NewGuid():N}");

        try
        {
            await using var context = new PvadDbContext(dbOptions);
            await context.Database.MigrateAsync();
            context.Users.Add(new PvadIdentityUser
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
            context.OrganizationSubscriptions.Add(new OrganizationSubscription
            {
                Id = subscriptionId,
                OrganizationId = organizationId,
                Edition = SubscriptionEdition.Enterprise,
                AccountType = SubscriptionAccountType.Company,
                Status = SubscriptionStatus.Active,
                BillingPeriod = SubscriptionBillingPeriod.Annual,
                DeploymentMode = SubscriptionDeploymentMode.OnPremise,
            });
            context.OfflineLicenses.Add(new OfflineLicense
            {
                OrganizationId = organizationId,
                SubscriptionId = subscriptionId,
                LicenseNumber = "PXA-METRICS-TEST",
                EnvelopeJson = "{}",
                Signature = "signature",
                KeyId = "test",
                Algorithm = "ECDSA_P256_SHA256",
                ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
                ValidUntil = DateTimeOffset.UtcNow.AddDays(5),
                IssuedByUserId = userId,
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
            context.BackgroundJobs.Add(new PvadBackgroundJob
            {
                Id = jobId,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PvadJobQueue.TemplateRenderType,
                TraceParent = traceParent,
                TraceState = "pxa=integration-test",
                RetentionMode = PvadJobRetentionMode.Retained,
                PayloadJson = JsonSerializer.Serialize(new TemplateRenderJobPayload(
                    templateId.ToString(),
                    JsonSerializer.SerializeToElement(new { invoiceNumber = "PXA-1" }),
                    null)),
            });
            await context.SaveChangesAsync();

            var queueMeasurements = new List<Measurement>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == PvadTelemetry.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                queueMeasurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
            listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                queueMeasurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
            listener.Start();

            var services = new ServiceCollection();
            services.AddDbContext<PvadDbContext>(builder =>
                builder.UseNpgsql(postgres.GetConnectionString()));
            await using var serviceProvider = services.BuildServiceProvider();
            var metricsPublisher = new PvadJobMetricsPublisher(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new PvadJobOptions()),
                NullLogger<PvadJobMetricsPublisher>.Instance);
            await metricsPublisher.PublishAsync(CancellationToken.None);
            var licensingPublisher = new PvadLicensingMetricsPublisher(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new PvadLicensingOptions()),
                NullLogger<PvadLicensingMetricsPublisher>.Instance);
            await licensingPublisher.PublishAsync(CancellationToken.None);

            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pvad.jobs.queue.depth" && value.Value == 1);
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pvad.jobs.queue.oldest.age" && value.Value >= 0);
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pvad.licensing.licenses" &&
                         value.Value == 1 &&
                         value.Tags.Any(tag =>
                             tag.Key == "license.state" &&
                             Equals(tag.Value, "expiring_14d")));

            var storageOptions = Options.Create(new PvadStorageOptions { RootPath = root });
            var storage = new FileSystemPvadObjectStorage(
                storageOptions,
                new TestWebHostEnvironment { ContentRootPath = root });
            var storedObjects = new PvadStoredObjectService(context, storage, storageOptions);
            var processor = new PvadJobProcessor(
                context,
                storedObjects,
                [],
                new PVAD.Application.UseCases.ExportDocumentUseCase(
                    Array.Empty<PVAD.Core.Abstractions.IDocumentExporter>()),
                new PVAD.WebApi.Services.MigrationService(),
                null!,
                Options.Create(new PvadJobOptions()),
                NullLogger<PvadJobProcessor>.Instance);

            var traceActivities = new ConcurrentBag<ActivitySnapshot>();
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == PvadTelemetry.ActivitySourceName ||
                    source.Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase),
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => traceActivities.Add(new ActivitySnapshot(
                    activity.Source.Name,
                    activity.OperationName,
                    activity.TraceId,
                    activity.SpanId,
                    activity.ParentSpanId,
                    activity.TagObjects.ToArray())),
            };
            ActivitySource.AddActivityListener(activityListener);

            Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

            Assert.True(ActivityContext.TryParse(
                traceParent,
                null,
                isRemote: true,
                out var persistedTraceContext));
            var processingActivity = Assert.Single(
                traceActivities,
                value => value.OperationName == "pvad.job.process");
            var documentActivity = Assert.Single(
                traceActivities,
                value => value.OperationName == "pvad.document.operation");
            Assert.Equal(persistedTraceContext.TraceId, processingActivity.TraceId);
            Assert.Equal(processingActivity.TraceId, documentActivity.TraceId);
            Assert.Equal(processingActivity.SpanId, documentActivity.ParentSpanId);
            Assert.Contains(
                traceActivities,
                value =>
                    value.Source.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) &&
                    value.TraceId == documentActivity.TraceId &&
                    value.ParentSpanId == documentActivity.SpanId);
            Assert.DoesNotContain(
                documentActivity.Tags,
                tag => PvadTelemetrySanitizingProcessor.IsForbiddenAttribute(tag.Key));

            context.ChangeTracker.Clear();
            var completed = await context.BackgroundJobs.SingleAsync(value => value.Id == jobId);
            Assert.Equal(PvadBackgroundJobStatus.Completed, completed.Status);
            Assert.Equal(traceParent, completed.TraceParent);
            Assert.Equal("pxa=integration-test", completed.TraceState);
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
            var retention = new PvadJobRetentionService(
                context,
                storedObjects,
                new PVAD.WebApi.Application.Retention.PvadRetentionLegalHoldService(context),
                Options.Create(new PvadJobOptions { CleanupBatchSize = 10 }));

            var hold = new RetentionLegalHold
            {
                Category = "background-document-jobs",
                OrganizationId = organizationId,
                Reason = "Preserve the completed result during the integration test.",
                CreatedByUserId = userId,
            };
            context.RetentionLegalHolds.Add(hold);
            await context.SaveChangesAsync();
            Assert.Equal(0, await retention.CleanupAsync(CancellationToken.None));
            Assert.Equal(
                PvadBackgroundJobStatus.Completed,
                (await context.BackgroundJobs.SingleAsync(value => value.Id == jobId)).Status);

            hold.ReleasedAt = DateTimeOffset.UtcNow;
            hold.ReleasedByUserId = userId;
            hold.ReleaseReason = "The integration-test preservation window has ended.";
            await context.SaveChangesAsync();
            Assert.Equal(1, await retention.CleanupAsync(CancellationToken.None));
            context.ChangeTracker.Clear();
            var expired = await context.BackgroundJobs.SingleAsync(value => value.Id == jobId);
            var deletedResult = await context.StoredObjects.SingleAsync();
            Assert.Equal(PvadBackgroundJobStatus.Expired, expired.Status);
            Assert.Null(expired.ResultObjectId);
            Assert.NotNull(expired.ContentPurgedAt);
            Assert.Equal("{}", expired.PayloadJson);
            Assert.Equal(PvadStoredObjectStatus.Deleted, deletedResult.Status);

            await using var transientContent = new MemoryStream("transient-result"u8.ToArray(), writable: false);
            var transientObject = await storedObjects.StoreAsync(
                organizationId,
                userId,
                "job-result",
                "application/octet-stream",
                "transient.bin",
                transientContent,
                CancellationToken.None);
            var transientJob = new PvadBackgroundJob
            {
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PvadJobQueue.DocumentExportType,
                PayloadJson = "{\"sensitive\":true}",
                Status = PvadBackgroundJobStatus.Completed,
                RetentionMode = PvadJobRetentionMode.Transient,
                ResultObjectId = transientObject.Id,
                CompletedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
                MetadataExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };
            context.BackgroundJobs.Add(transientJob);
            await context.SaveChangesAsync();

            Assert.True(await retention.PurgeTransientContentAfterDownloadAsync(
                transientJob.Id,
                organizationId,
                CancellationToken.None));
            context.ChangeTracker.Clear();
            var downloaded = await context.BackgroundJobs.SingleAsync(value => value.Id == transientJob.Id);
            Assert.Equal(PvadBackgroundJobStatus.Completed, downloaded.Status);
            Assert.NotNull(downloaded.ResultDownloadedAt);
            Assert.NotNull(downloaded.ContentPurgedAt);
            Assert.Null(downloaded.ResultObjectId);
            Assert.Equal("{}", downloaded.PayloadJson);
            Assert.Equal(
                PvadStoredObjectStatus.Deleted,
                (await context.StoredObjects.SingleAsync(value => value.Id == transientObject.Id)).Status);

            var codeJob = new PvadBackgroundJob
            {
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PvadJobQueue.DesignerCodeExecutionType,
                PayloadJson = "{\"sourceChecksum\":\"sha256-only\"}",
                DiagnosticsJson = "{\"diagnosticCodes\":[\"PXACODE030\"]}",
                Status = PvadBackgroundJobStatus.Completed,
                RetentionMode = PvadJobRetentionMode.Transient,
                CompletedAt = DateTimeOffset.UtcNow.AddHours(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                MetadataExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            };
            context.BackgroundJobs.Add(codeJob);
            await context.SaveChangesAsync();

            Assert.Equal(1, await retention.CleanupAsync(CancellationToken.None));
            context.ChangeTracker.Clear();
            var purgedCodeJob = await context.BackgroundJobs.SingleAsync(value => value.Id == codeJob.Id);
            Assert.Equal(PvadBackgroundJobStatus.Expired, purgedCodeJob.Status);
            Assert.Equal("{}", purgedCodeJob.PayloadJson);
            Assert.Null(purgedCodeJob.DiagnosticsJson);
            Assert.NotNull(purgedCodeJob.ContentPurgedAt);

            purgedCodeJob.MetadataExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
            Assert.Equal(0, await retention.CleanupAsync(CancellationToken.None));
            context.ChangeTracker.Clear();
            Assert.False(await context.BackgroundJobs.AnyAsync(value => value.Id == codeJob.Id));
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pvad.storage.operations");
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pvad.storage.bytes" && value.Value > 0);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed record Measurement(
        string Name,
        double Value,
        KeyValuePair<string, object?>[] Tags);

    private sealed record ActivitySnapshot(
        string Source,
        string OperationName,
        ActivityTraceId TraceId,
        ActivitySpanId SpanId,
        ActivitySpanId ParentSpanId,
        KeyValuePair<string, object?>[] Tags);

    private sealed record TestTenantContext(Guid CurrentUserId, Guid CurrentOrganizationId)
        : PVAD.WebApi.Security.IPvadTenantContext
    {
        public Guid? UserId => CurrentUserId;
        public Guid? OrganizationId => CurrentOrganizationId;
    }

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
