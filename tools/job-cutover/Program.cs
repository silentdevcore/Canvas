using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
#if LEGACY
using PXA.Domain.Entities;
using PXA.Core.Contracts;
using PXA.FileImporter;
using PXA.WebApi.Services;
using Db = PXA.Infrastructure.Persistence.PxaDbContext;
using User = PXA.Infrastructure.Persistence.Identity.PxaIdentityUser;
using Role = PXA.Infrastructure.Persistence.Identity.PxaIdentityRole;
using Job = PXA.Domain.Entities.PxaBackgroundJob;
using Status = PXA.Domain.Entities.PxaBackgroundJobStatus;
using Objects = PXA.WebApi.Services.Storage.PxaStoredObjectService;
using Storage = PXA.WebApi.Services.Storage.FileSystemPxaObjectStorage;
using StorageOptions = PXA.WebApi.Services.Storage.PxaStorageOptions;
using Processor = PXA.WebApi.Services.Jobs.PxaJobProcessor;
using JobOptions = PXA.WebApi.Services.Jobs.PxaJobOptions;
using JobController = PXA.WebApi.Controllers.JobsController;
using TenantContext = PXA.WebApi.Security.IPxaTenantContext;
using ExportUseCase = PXA.Application.UseCases.ExportDocumentUseCase;
#else
using PVAD.Domain.Entities;
using PVAD.Core.Contracts;
using PVAD.FileImporter;
using PVAD.WebApi.Services;
using Db = PVAD.Infrastructure.Persistence.PvadDbContext;
using User = PVAD.Infrastructure.Persistence.Identity.PvadIdentityUser;
using Role = PVAD.Infrastructure.Persistence.Identity.PvadIdentityRole;
using Job = PVAD.Domain.Entities.PvadBackgroundJob;
using Status = PVAD.Domain.Entities.PvadBackgroundJobStatus;
using Objects = PVAD.WebApi.Services.Storage.PvadStoredObjectService;
using Storage = PVAD.WebApi.Services.Storage.FileSystemPvadObjectStorage;
using StorageOptions = PVAD.WebApi.Services.Storage.PvadStorageOptions;
using Processor = PVAD.WebApi.Services.Jobs.PvadJobProcessor;
using JobOptions = PVAD.WebApi.Services.Jobs.PvadJobOptions;
using JobController = PVAD.WebApi.Controllers.JobsController;
using TenantContext = PVAD.WebApi.Security.IPvadTenantContext;
using ExportUseCase = PVAD.Application.UseCases.ExportDocumentUseCase;
#endif

internal static class Cutover
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var connection = Environment.GetEnvironmentVariable("PVAD_CUTOVER_CONNECTION") ?? "";
            var root = Environment.GetEnvironmentVariable("PVAD_CUTOVER_OBJECT_ROOT") ?? "";
            if (Environment.GetEnvironmentVariable("PVAD_CUTOVER_TEST_ALLOW") != "1" ||
                new NpgsqlConnectionStringBuilder(connection).Database != "pvad_cutover_test" ||
                !Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(), "pvad-binary-cutover-"), StringComparison.Ordinal))
                throw new InvalidOperationException("Isolated test database and temporary object root are required.");
            await using var db = new Db(new DbContextOptionsBuilder<Db>().UseNpgsql(connection).Options);
#if LEGACY
            Require(typeof(Processor).Assembly.GetName().Name == "PXA.WebApi");
#else
            Require(typeof(Processor).Assembly.GetName().Name == "PVAD.WebApi");
#endif
            var storageOptions = Options.Create(new StorageOptions { RootPath = root });
            var objects = new Objects(db, new Storage(storageOptions, new HostEnvironment()), storageOptions);
            var statePath = Path.Combine(root, "fixture-state.json");
            if (args is ["seed"])
            {
                await db.Database.MigrateAsync();
                var organization = new Organization { Name = "Synthetic cutover", Slug = "synthetic-cutover" };
                var user = new User { Id = Guid.NewGuid(), DisplayName = "Synthetic cutover" };
                db.AddRange(organization, user);
                var role = new Role { Id = Guid.NewGuid(), Name = "synthetic-retained-role" };
                var membership = new OrganizationMembership { OrganizationId = organization.Id, UserId = user.Id };
                var subscription = new OrganizationSubscription
                {
                    OrganizationId = organization.Id, Edition = SubscriptionEdition.Enterprise,
                    AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active,
                    DeploymentMode = SubscriptionDeploymentMode.OnPremise, SeatLimit = 2,
                };
                var entitlement = new SubscriptionEntitlement
                {
                    SubscriptionId = subscription.Id, Capability = "designer", Enabled = true, Limit = 100,
                };
                const string draft = "{\"Id\":\"legacy-document\",\"Name\":\"PXA retained template\",\"Pages\":[]}";
                var template = new DesignerTemplate
                {
                    OrganizationId = organization.Id, CreatedByUserId = user.Id, UpdatedByUserId = user.Id,
                    Name = "PXA retained template", DraftJson = draft, Revision = 7,
                    DraftChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(draft))).ToLowerInvariant(),
                    SchemaVersion = "1", DesignerVersion = "1.1.0",
                };
                db.AddRange(role, membership, subscription, entitlement, template,
                    new OrganizationMembershipRole { OrganizationMembershipId = membership.Id, RoleId = role.Id });
                await db.SaveChangesAsync();
                await using var bytes = new MemoryStream([1, 2, 3]);
                var input = await objects.StoreAsync(organization.Id, user.Id, "job-input",
                    "application/octet-stream", "source.pxa.json", bytes, default);
                Job NewJob(DateTimeOffset scheduled) => new()
                {
                    OrganizationId = organization.Id, CreatedByUserId = user.Id, Type = "document.import",
                    PayloadJson = "{\"Extension\":\"test\",\"Name\":\"PXA retained source\"}",
                    InputObjectId = input.Id, ScheduledAt = scheduled,
                };
                var interrupted = NewJob(DateTimeOffset.UtcNow.AddMinutes(-1));
                var cancelled = NewJob(DateTimeOffset.UtcNow.AddHours(1));
                db.AddRange(interrupted, cancelled);
                await db.SaveChangesAsync();
                await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new Fixture(
                    organization.Id, user.Id, input.Id, interrupted.Id, cancelled.Id,
                    (await db.Database.GetAppliedMigrationsAsync()).ToArray(), input.ObjectKey, input.Checksum,
                    role.Id, membership.Id, subscription.Id, entitlement.Id, template.Id, template.DraftChecksum)));
                Console.WriteLine("SEEDED");
                return 0;
            }
            var fixture = JsonSerializer.Deserialize<Fixture>(await File.ReadAllTextAsync(statePath))!;
            switch (args.Single())
            {
                case "pause":
                case "process":
                    var processor = new Processor(db, objects, [new Importer(args[0] == "pause")],
                        new ExportUseCase([]), new MigrationService(), null!, Options.Create(new JobOptions()),
                        NullLogger<Processor>.Instance);
                    Require(await processor.ProcessNextAsync(default));
                    Console.WriteLine("PROCESSED");
                    break;
                case "expire":
                    Require(await db.BackgroundJobs.Where(value => value.Id == fixture.InterruptedId && value.Status == Status.Processing)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.LeaseExpiresAt,
                            DateTimeOffset.UtcNow.AddMinutes(-1))) == 1);
                    Console.WriteLine("EXPIRED_FOR_TEST");
                    break;
                case "cancel":
                    var controller = new JobController(db, new Tenant(fixture.UserId, fixture.OrganizationId), objects,
                        null!, Options.Create(new JobOptions()));
                    await controller.Cancel(fixture.CancelledId, default);
                    Require(await db.BackgroundJobs.AsNoTracking().AnyAsync(value =>
                        value.Id == fixture.CancelledId && value.Status == Status.Cancelled));
                    Console.WriteLine("CANCELLED");
                    break;
                case "verify":
                    var completed = await db.BackgroundJobs.AsNoTracking().SingleAsync(value => value.Id == fixture.InterruptedId);
                    Require(completed.Status == Status.Completed && completed.Attempts == 2 &&
                        completed.Type == "document.import" && completed.InputObjectId == fixture.InputId &&
                        completed.OrganizationId == fixture.OrganizationId && completed.CreatedByUserId == fixture.UserId);
                    Require(await db.BackgroundJobs.AnyAsync(value => value.Id == fixture.CancelledId && value.Status == Status.Cancelled));
                    Require(fixture.Migrations.SequenceEqual(await db.Database.GetAppliedMigrationsAsync()));
                    Require(!(await db.Database.GetPendingMigrationsAsync()).Any());
                    Require(await db.Organizations.AnyAsync(value => value.Id == fixture.OrganizationId));
                    Require(await db.Users.AnyAsync(value => value.Id == fixture.UserId));
                    Require(await db.Roles.AnyAsync(value => value.Id == fixture.RoleId && value.Name == "synthetic-retained-role"));
                    Require(await db.OrganizationMemberships.AnyAsync(value => value.Id == fixture.MembershipId &&
                        value.OrganizationId == fixture.OrganizationId && value.UserId == fixture.UserId));
                    Require(await db.OrganizationMembershipRoles.AnyAsync(value => value.OrganizationMembershipId == fixture.MembershipId && value.RoleId == fixture.RoleId));
                    Require(await db.OrganizationSubscriptions.AnyAsync(value => value.Id == fixture.SubscriptionId &&
                        value.OrganizationId == fixture.OrganizationId && value.Edition == SubscriptionEdition.Enterprise && value.SeatLimit == 2));
                    Require(await db.SubscriptionEntitlements.AnyAsync(value => value.Id == fixture.EntitlementId &&
                        value.SubscriptionId == fixture.SubscriptionId && value.Capability == "designer" && value.Enabled && value.Limit == 100));
                    var retainedTemplate = await db.DesignerTemplates.AsNoTracking().SingleAsync(value => value.Id == fixture.TemplateId);
                    Require(retainedTemplate.OrganizationId == fixture.OrganizationId && retainedTemplate.CreatedByUserId == fixture.UserId &&
                        retainedTemplate.Revision == 7 && retainedTemplate.Name == "PXA retained template" && retainedTemplate.DraftChecksum == fixture.DraftChecksum);
                    using (var draft = JsonDocument.Parse(retainedTemplate.DraftJson))
                        Require(draft.RootElement.GetProperty("Id").GetString() == "legacy-document");
                    var input = await objects.OpenAsync(fixture.InputId, fixture.OrganizationId, default);
                    Require(input is not null && input.Value.Metadata.FileName == "source.pxa.json");
                    Require(input!.Value.Metadata.ObjectKey == fixture.ObjectKey && input.Value.Metadata.Checksum == fixture.InputChecksum);
                    await using (var content = input!.Value.Content) Require(content.ReadByte() == 1);
                    var result = await objects.OpenAsync(completed.ResultObjectId!.Value, fixture.OrganizationId, default);
                    Require(result is not null);
                    await using (var content = result!.Value.Content)
                        Require((await JsonSerializer.DeserializeAsync<DesignExportDto>(content))?.Name == "PXA retained source");
                    Require(await objects.OpenAsync(completed.ResultObjectId.Value, Guid.NewGuid(), default) is null);
                    Console.WriteLine("VERIFIED");
                    break;
                default: throw new InvalidOperationException("Unknown test operation.");
            }
            return 0;
        }
        catch (Exception exception)
        {
            // Process failures must not expose connection strings or document payloads.
            Console.Error.WriteLine("CUTOVER_FAILED:" + exception.GetType().Name);
            return 1;
        }
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Synthetic cutover assertion failed.");
    }
    private sealed record Fixture(Guid OrganizationId, Guid UserId, Guid InputId, Guid InterruptedId,
        Guid CancelledId, string[] Migrations, string ObjectKey, string InputChecksum,
        Guid RoleId, Guid MembershipId, Guid SubscriptionId, Guid EntitlementId, Guid TemplateId, string DraftChecksum);
    private sealed record Tenant(Guid? UserId, Guid? OrganizationId) : TenantContext;
    private sealed class Importer(bool pause) : IFileImporter
    {
        public IReadOnlyList<string> SupportedExtensions => ["test"];
        public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null)
        {
            if (pause)
            {
                Console.WriteLine("CLAIMED");
                await Console.Out.FlushAsync();
                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
            return new() { Name = name ?? "Synthetic" };
        }
    }
    private sealed class HostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PVAD.JobCutover";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
