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
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Observability;
using PXA.WebApi.Services.Jobs;
using PXA.WebApi.Services.Licensing;
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
        var subscriptionId = Guid.NewGuid();
        var traceParent = $"00-{ActivityTraceId.CreateRandom()}-{ActivitySpanId.CreateRandom()}-01";
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
            context.BackgroundJobs.Add(new PxaBackgroundJob
            {
                Id = jobId,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                Type = PxaJobQueue.TemplateRenderType,
                TraceParent = traceParent,
                TraceState = "pxa=integration-test",
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
                if (instrument.Meter.Name == PxaTelemetry.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                queueMeasurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
            listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                queueMeasurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
            listener.Start();

            var services = new ServiceCollection();
            services.AddDbContext<PxaDbContext>(builder =>
                builder.UseNpgsql(postgres.GetConnectionString()));
            await using var serviceProvider = services.BuildServiceProvider();
            var metricsPublisher = new PxaJobMetricsPublisher(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new PxaJobOptions()),
                NullLogger<PxaJobMetricsPublisher>.Instance);
            await metricsPublisher.PublishAsync(CancellationToken.None);
            var licensingPublisher = new PxaLicensingMetricsPublisher(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new PxaLicensingOptions()),
                NullLogger<PxaLicensingMetricsPublisher>.Instance);
            await licensingPublisher.PublishAsync(CancellationToken.None);

            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pxa.jobs.queue.depth" && value.Value == 1);
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pxa.jobs.queue.oldest.age" && value.Value >= 0);
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pxa.licensing.licenses" &&
                         value.Value == 1 &&
                         value.Tags.Any(tag =>
                             tag.Key == "license.state" &&
                             Equals(tag.Value, "expiring_14d")));

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

            var traceActivities = new ConcurrentBag<ActivitySnapshot>();
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == PxaTelemetry.ActivitySourceName ||
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
                value => value.OperationName == "pxa.job.process");
            var documentActivity = Assert.Single(
                traceActivities,
                value => value.OperationName == "pxa.document.operation");
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
                tag => PxaTelemetrySanitizingProcessor.IsForbiddenAttribute(tag.Key));

            context.ChangeTracker.Clear();
            var completed = await context.BackgroundJobs.SingleAsync(value => value.Id == jobId);
            Assert.Equal(PxaBackgroundJobStatus.Completed, completed.Status);
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
            var retention = new PxaJobRetentionService(
                context,
                storedObjects,
                new PXA.WebApi.Application.Retention.PxaRetentionLegalHoldService(context),
                Options.Create(new PxaJobOptions { CleanupBatchSize = 10 }));

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
                PxaBackgroundJobStatus.Completed,
                (await context.BackgroundJobs.SingleAsync(value => value.Id == jobId)).Status);

            hold.ReleasedAt = DateTimeOffset.UtcNow;
            hold.ReleasedByUserId = userId;
            hold.ReleaseReason = "The integration-test preservation window has ended.";
            await context.SaveChangesAsync();
            Assert.Equal(1, await retention.CleanupAsync(CancellationToken.None));
            context.ChangeTracker.Clear();
            var expired = await context.BackgroundJobs.SingleAsync(value => value.Id == jobId);
            var deletedResult = await context.StoredObjects.SingleAsync();
            Assert.Equal(PxaBackgroundJobStatus.Expired, expired.Status);
            Assert.Null(expired.ResultObjectId);
            Assert.Equal(PxaStoredObjectStatus.Deleted, deletedResult.Status);
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pxa.storage.operations");
            Assert.Contains(
                queueMeasurements,
                value => value.Name == "pxa.storage.bytes" && value.Value > 0);
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
