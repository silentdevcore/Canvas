using System.Text.Json;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.Core.Contracts;
using PVAD.Domain.Entities;
using PVAD.FileImporter;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services;
using PVAD.WebApi.Services.Jobs;
using PVAD.WebApi.Services.Storage;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class PvadJobCutoverCompatibilityTests
{
    [PostgreSqlFact]
    public async Task Restart_preserves_payload_paths_and_limits_and_pending_cancellation_is_tenant_scoped()
    {
        await using var harness = await Harness.CreateAsync();
        var expired = await harness.SeedAsync(PvadBackgroundJobStatus.Processing, 1, true);
        await using (var worker = new PvadDbContext(harness.Options))
            Assert.True(await harness.Processor(worker).ProcessNextAsync(default));
        await using var verify = new PvadDbContext(harness.Options);
        var completed = await verify.BackgroundJobs.AsNoTracking().SingleAsync(value => value.Id == expired);
        Assert.Equal(PvadBackgroundJobStatus.Completed, completed.Status);
        Assert.Equal(2, completed.Attempts);
        Assert.Equal("document.import", completed.Type);
        using var payload = JsonDocument.Parse(completed.PayloadJson);
        Assert.Equal("PXA retained source", payload.RootElement.GetProperty("Name").GetString());
        var objects = harness.Objects(verify);
        var input = await objects.OpenAsync(harness.InputId, harness.OrganizationId, default);
        Assert.NotNull(input);
        await using (var stream = input.Value.Content)
            Assert.Equal(1, stream.ReadByte());
        Assert.Equal("source.pxa.json", input.Value.Metadata.FileName);
        Assert.Null(await objects.OpenAsync(completed.ResultObjectId!.Value, Guid.NewGuid(), default));
        var result = await objects.OpenAsync(completed.ResultObjectId.Value, harness.OrganizationId, default);
        Assert.NotNull(result);
        await using (var stream = result.Value.Content)
        {
            var design = await JsonSerializer.DeserializeAsync<DesignExportDto>(stream);
            Assert.Equal("PXA retained source", design!.Name);
        }

        var active = await harness.SeedAsync(PvadBackgroundJobStatus.Processing, 1, false);
        var exhausted = await harness.SeedAsync(PvadBackgroundJobStatus.Processing, 3, true);
        var pending = await harness.SeedAsync(PvadBackgroundJobStatus.Pending, 0, false);
        await using (var cancelDb = new PvadDbContext(harness.Options))
        {
            var foreign = harness.Controller(cancelDb, Guid.NewGuid());
            Assert.IsType<NotFoundResult>((await foreign.Cancel(pending, default)).Result);
            var owner = harness.Controller(cancelDb, harness.OrganizationId);
            Assert.Equal("Cancelled", Assert.IsType<PvadJobResponse>(Assert.IsType<OkObjectResult>(
                (await owner.Cancel(pending, default)).Result).Value).Status);
        }
        var racing = await harness.SeedAsync(PvadBackgroundJobStatus.Pending, 0, false);
        var claimedLease = Guid.NewGuid();
        var cancelOptions = new DbContextOptionsBuilder<PvadDbContext>(harness.Options)
            .AddInterceptors(new BeforeCancel(async () =>
            {
                await using var claimer = new PvadDbContext(harness.Options);
                await claimer.BackgroundJobs.Where(value => value.Id == racing).ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.Status, PvadBackgroundJobStatus.Processing)
                    .SetProperty(value => value.LeaseId, claimedLease)
                    .SetProperty(value => value.LeaseExpiresAt, DateTimeOffset.UtcNow.AddMinutes(5)));
            })).Options;
        await using (var cancelDb = new PvadDbContext(cancelOptions))
            Assert.Equal("Processing", Assert.IsType<PvadJobResponse>(Assert.IsType<OkObjectResult>(
                (await harness.Controller(cancelDb, harness.OrganizationId).Cancel(racing, default)).Result).Value).Status);
        await using (var restarted = new PvadDbContext(harness.Options))
            Assert.False(await harness.Processor(restarted).ProcessNextAsync(default));
        verify.ChangeTracker.Clear();
        var exhaustedJob = await verify.BackgroundJobs.SingleAsync(value => value.Id == exhausted);
        Assert.Equal(PvadBackgroundJobStatus.DeadLetter, exhaustedJob.Status);
        Assert.Equal(3, exhaustedJob.Attempts);
        Assert.NotNull(exhaustedJob.CompletedAt);
        Assert.Null(exhaustedJob.LeaseId);
        Assert.Equal(PvadBackgroundJobStatus.Processing, (await verify.BackgroundJobs.SingleAsync(value => value.Id == active)).Status);
        Assert.Equal(PvadBackgroundJobStatus.Cancelled, (await verify.BackgroundJobs.SingleAsync(value => value.Id == pending)).Status);
        Assert.Equal(claimedLease, (await verify.BackgroundJobs.SingleAsync(value => value.Id == racing)).LeaseId);
    }

    [PostgreSqlFact]
    public async Task Old_worker_cannot_publish_after_reclaim_or_cancellation_and_shutdown_is_recoverable()
    {
        await using var harness = await Harness.CreateAsync();
        foreach (var cancelled in new[] { false, true })
        {
            var jobId = await harness.SeedAsync(PvadBackgroundJobStatus.Pending, 0, false);
            var newLease = Guid.NewGuid();
            var importer = new Importer(async (_, _) =>
            {
                await using var concurrent = new PvadDbContext(harness.Options);
                await concurrent.BackgroundJobs.Where(value => value.Id == jobId).ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.Status, cancelled ? PvadBackgroundJobStatus.Cancelled : PvadBackgroundJobStatus.Processing)
                    .SetProperty(value => value.LeaseId, cancelled ? (Guid?)null : newLease)
                    .SetProperty(value => value.LeaseExpiresAt, DateTimeOffset.UtcNow.AddMinutes(5)));
            });
            await using (var worker = new PvadDbContext(harness.Options))
                Assert.True(await harness.Processor(worker, importer).ProcessNextAsync(default));
            await using var verify = new PvadDbContext(harness.Options);
            var persisted = await verify.BackgroundJobs.SingleAsync(value => value.Id == jobId);
            Assert.Equal(cancelled ? PvadBackgroundJobStatus.Cancelled : PvadBackgroundJobStatus.Processing, persisted.Status);
            Assert.Null(persisted.ResultObjectId);
            Assert.Equal(cancelled ? (Guid?)null : newLease, persisted.LeaseId);
            Assert.All(await verify.StoredObjects.Where(value => value.Purpose == "job-result").ToArrayAsync(),
                value => Assert.Equal(PvadStoredObjectStatus.Deleted, value.Status));
            if (!cancelled)
                await verify.BackgroundJobs.Where(value => value.Id == jobId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, PvadBackgroundJobStatus.Cancelled));
        }

        var shutdownJob = await harness.SeedAsync(PvadBackgroundJobStatus.Pending, 0, false);
        using var stopping = new CancellationTokenSource();
        var shutdownImporter = new Importer((_, token) =>
        {
            stopping.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask;
        });
        await using (var interrupted = new PvadDbContext(harness.Options))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Processor(interrupted, shutdownImporter).ProcessNextAsync(stopping.Token));
        await using (var recovery = new PvadDbContext(harness.Options))
        {
            var interrupted = await recovery.BackgroundJobs.AsNoTracking().SingleAsync(value => value.Id == shutdownJob);
            Assert.Equal(PvadBackgroundJobStatus.Processing, interrupted.Status);
            Assert.Null(interrupted.ResultObjectId);
            await recovery.BackgroundJobs.Where(value => value.Id == shutdownJob).ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.LeaseExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        await using (var restarted = new PvadDbContext(harness.Options))
            Assert.True(await harness.Processor(restarted).ProcessNextAsync(default));
        await using var final = new PvadDbContext(harness.Options);
        var resumed = await final.BackgroundJobs.SingleAsync(value => value.Id == shutdownJob);
        Assert.Equal(PvadBackgroundJobStatus.Completed, resumed.Status);
        Assert.Equal(2, resumed.Attempts);
    }

    private sealed class BeforeCancel(Func<Task> claim) : DbCommandInterceptor
    {
        private int invoked;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref invoked, 1) == 0) await claim();
            return result;
        }
    }

    private sealed class Importer(Func<string?, CancellationToken, Task>? action = null) : IFileImporter
    {
        public IReadOnlyList<string> SupportedExtensions => ["test"];
        public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) => ImportAsync(stream, name, default);
        public async Task<DesignExportDto> ImportAsync(Stream stream, string? name, CancellationToken cancellationToken)
        {
            if (action is not null) await action(name, cancellationToken);
            return new() { Name = name ?? "Synthetic" };
        }
    }

    private sealed record Tenant(Guid? UserId, Guid? OrganizationId) : IPvadTenantContext;
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PVAD.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        private readonly string root = Path.Combine(Path.GetTempPath(), "pvad-job-cutover-" + Guid.NewGuid().ToString("N"));
        public DbContextOptions<PvadDbContext> Options { get; private set; } = null!;
        public Guid OrganizationId { get; } = Guid.NewGuid();
        private Guid UserId { get; } = Guid.NewGuid();
        public Guid InputId { get; private set; }
        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness();
            try
            {
                await harness.postgres.StartAsync();
                harness.Options = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(harness.postgres.GetConnectionString()).Options;
                await using var db = new PvadDbContext(harness.Options);
                await db.Database.EnsureCreatedAsync();
                db.AddRange(new Organization { Id = harness.OrganizationId, Name = "Synthetic", Slug = "synthetic" },
                    new PvadIdentityUser { Id = harness.UserId, DisplayName = "Synthetic" });
                await db.SaveChangesAsync();
                await using var bytes = new MemoryStream([1, 2, 3]);
                harness.InputId = (await harness.Objects(db).StoreAsync(harness.OrganizationId, harness.UserId,
                    "job-input", "application/octet-stream", "source.pxa.json", bytes, default)).Id;
                return harness;
            }
            catch { await harness.DisposeAsync(); throw; }
        }
        public async Task<Guid> SeedAsync(PvadBackgroundJobStatus status, int attempts, bool expired)
        {
            await using var db = new PvadDbContext(Options);
            var job = new PvadBackgroundJob
            {
                OrganizationId = OrganizationId, CreatedByUserId = UserId, Type = "document.import",
                PayloadJson = "{\"Extension\":\"test\",\"Name\":\"PXA retained source\"}",
                InputObjectId = InputId, Status = status, Attempts = attempts,
                ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                LeaseId = status == PvadBackgroundJobStatus.Processing ? Guid.NewGuid() : null,
                LeaseExpiresAt = status == PvadBackgroundJobStatus.Processing
                    ? DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 5) : null,
            };
            db.Add(job); await db.SaveChangesAsync(); return job.Id;
        }
        public PvadStoredObjectService Objects(PvadDbContext db)
        {
            var options = Microsoft.Extensions.Options.Options.Create(new PvadStorageOptions { RootPath = root });
            return new(db, new FileSystemPvadObjectStorage(options, new EnvironmentStub()), options);
        }
        public PvadJobProcessor Processor(PvadDbContext db, IFileImporter? importer = null) => new(db, Objects(db),
            [importer ?? new Importer()], new PVAD.Application.UseCases.ExportDocumentUseCase([]), new MigrationService(),
            null!, Microsoft.Extensions.Options.Options.Create(new PvadJobOptions()), NullLogger<PvadJobProcessor>.Instance);
        public JobsController Controller(PvadDbContext db, Guid organizationId) => new(db, new Tenant(UserId, organizationId),
            Objects(db), null!, Microsoft.Extensions.Options.Options.Create(new PvadJobOptions()));
        public async ValueTask DisposeAsync()
        {
            await postgres.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
