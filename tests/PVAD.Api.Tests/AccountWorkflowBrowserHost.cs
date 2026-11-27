using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.Licensing;
using PVAD.WebApi.Application.Legal;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;
using PVAD.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

// This fixture is compiled into the test assembly, not into the application.
// Privileged fixture controls use inherited stdin only; no test HTTP endpoints.
public static class AccountWorkflowBrowserHost
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static void Reply(string id, object result) => Console.WriteLine("PVAD_ACCOUNT_LIVE " + JsonSerializer.Serialize(new { id, result }, Json));

    public static async Task RunAsync()
    {
        try
        {
            var boot = JsonSerializer.Deserialize<Boot>(await Console.In.ReadLineAsync() ?? "", Json)
                ?? throw new InvalidOperationException();
            if (!Guid.TryParseExact(boot.RunId, "N", out _) ||
                !Path.GetFileName(boot.WorkDirectory).StartsWith("pvad-account-live-", StringComparison.Ordinal) ||
                Path.GetFullPath(Path.GetDirectoryName(boot.WorkDirectory)!) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
                await File.ReadAllTextAsync(Path.Combine(boot.WorkDirectory, "owner.txt")) != boot.RunId)
                throw new InvalidOperationException("Synthetic workspace ownership mismatch.");
            foreach (var origin in new[] { boot.AccountUrl, boot.DesignerUrl })
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Host != "localhost" || uri.Port < 1024 || uri.Scheme != "http")
                    throw new InvalidOperationException("Only isolated loopback origins are supported.");
            if (!Path.GetFullPath(boot.PrivateKeyPath).StartsWith(boot.WorkDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !File.Exists(Path.Combine(boot.WorkerPath, "PVAD.CodeWorker.dll"))) throw new InvalidOperationException();
            var activation = PVAD.Testing.PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH") ?? throw new InvalidOperationException();
            if (!Path.GetFullPath(activation).StartsWith(boot.WorkDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException();
            PvadLicense.LoadFromFile(activation);

            await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithLabel("pvade.account-live", boot.RunId).Build();
            await postgres.StartAsync();
            var databaseOptions = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
            await using (var db = new PvadDbContext(databaseOptions)) await db.Database.MigrateAsync();
            var actorId = Guid.NewGuid();
            var clock = new IssuerClock();
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PvadDatabase"] = postgres.GetConnectionString(),
                    ["DataProtection:KeysDirectory"] = Path.Combine(boot.WorkDirectory, "protection"),
                    ["Storage:RootPath"] = Path.Combine(boot.WorkDirectory, "objects"),
                    ["PdfViewer:AnnotationStoragePath"] = Path.Combine(boot.WorkDirectory, "viewer"),
                    ["PdfTrust:RevocationEvidence:CacheDirectory"] = Path.Combine(boot.WorkDirectory, "cache"),
                    ["Licensing:PrivateKeyPath"] = Path.Combine(boot.WorkDirectory, "server-private.pem"),
                    ["Licensing:PublicKeyPath"] = Path.Combine(boot.WorkDirectory, "server-public.pem"),
                    ["Observability:Enabled"] = "false", ["OAuth:Enabled"] = "false",
                    ["Mail:Enabled"] = "true", ["Mail:Transport"] = "Development",
                    ["Mail:AccountBaseUrl"] = boot.AccountUrl, ["Mail:DesignerBaseUrl"] = boot.DesignerUrl,
                    ["Registration:RequireDatabaseLegalDocuments"] = "true",
                    ["Registration:AllowedReturnOrigins:30"] = boot.DesignerUrl,
                    ["Registration:AllowedReturnOrigins:31"] = boot.AccountUrl,
                    ["DesignerAuthentication:AllowedOrigins:30"] = boot.DesignerUrl,
                    ["Cors:AllowedOrigins:30"] = boot.DesignerUrl, ["Cors:AllowedOrigins:31"] = boot.AccountUrl,
                    ["CodeWorker:Enabled"] = "true", ["CodeWorker:Hardened"] = "false", ["CodeWorker:WorkerPath"] = boot.WorkerPath,
                    ["NativeLicenseIssuer:Enabled"] = "true", ["NativeLicenseIssuer:KeyId"] = "ephemeral-native-engine-test",
                    ["NativeLicenseIssuer:PrivateKeyPath"] = boot.PrivateKeyPath,
                    ["NativeLicenseIssuer:AllowedUserIds:0"] = actorId.ToString(),
                    ["NativeLicenseAutomation:Enabled"] = "true", ["NativeLicenseAutomation:FreeRenewalEnabled"] = "true",
                    ["NativeLicenseAutomation:IssuerUserId"] = actorId.ToString(),
                    ["NativeLicenseAutomation:PolicyVersion"] = "synthetic-browser-free-three-months-v1",
                    ["NativeLicenseAutomation:Model"] = "Subscription", ["NativeLicenseAutomation:ValidityMonths"] = "3",
                }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                    services.RemoveAll<DbContextOptions<PvadDbContext>>();
                    services.AddDbContext<PvadDbContext>(options => options.UseNpgsql(postgres.GetConnectionString()));
                    services.AddScoped(provider => new PvadNativeLicenseIssuanceService(provider.GetRequiredService<PvadDbContext>(),
                        provider.GetRequiredService<PvadNativeLicenseSigner>(), provider.GetRequiredService<IOptions<PvadNativeIssuerOptions>>(),
                        provider.GetRequiredService<IOptions<PvadStepUpOptions>>(), provider.GetRequiredService<PvadSystemOperatorAccess>(), clock));
                });
            });
            factory.UseKestrel(0);
            var services = factory.Services;
            await SeedAsync(services, actorId);
            var address = services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Reply("boot", new { apiUrl = address, realKestrel = true, realPostgres = true, realSigner = true, developmentMail = true });

            while (await Console.In.ReadLineAsync() is { } line)
            {
                var command = JsonSerializer.Deserialize<Command>(line, Json) ?? throw new InvalidOperationException();
                if (command.Action == "stop") { Reply(command.Id, new { stopped = true }); break; }
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                object result;
                switch (command.Action)
                {
                    case "reset-clock": clock.Now = null; result = new { reset = true }; break;
                    case "mail":
                        if (command.Email?.EndsWith("@account-live.test", StringComparison.Ordinal) != true) throw new InvalidOperationException();
                        await scope.ServiceProvider.GetRequiredService<PvadMailProcessor>().ProcessPendingAsync(default);
                        var body = services.GetRequiredService<DevelopmentMailTransport>().Messages
                            .Last(message => message.RecipientEmail == command.Email && message.TextBody.Contains("/verify-email?", StringComparison.Ordinal)).TextBody;
                        var url = Regex.Match(body, @"http://localhost:\d+/verify-email\?[^\s<>""']+").Value;
                        if (!url.StartsWith(boot.AccountUrl + "/verify-email?", StringComparison.Ordinal)) throw new InvalidOperationException();
                        result = new { url }; break;
                    case "process":
                        var processor = scope.ServiceProvider.GetRequiredService<PvadNativeLicenseAutomationProcessor>();
                        var processed = 0;
                        while (await processor.ProcessNextAsync(default)) if (++processed > 10) throw new InvalidOperationException();
                        result = new { processed }; break;
                    case "expire":
                        var row = await db.NativeLibraryLicenses.SingleAsync(value => value.Id == command.LicenseId && value.IssuedByUserId == actorId);
                        clock.Now = row.ValidUntil!.Value;
                        result = new { expiredAt = clock.Now }; break;
                    case "offline":
                        if (command.Path is null || !Path.GetFullPath(command.Path).StartsWith(boot.WorkDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                            throw new InvalidOperationException();
                        var bytes = await File.ReadAllBytesAsync(command.Path);
                        var verifier = PvadLicense.CreateEmbeddedVerifier();
                        var payload = verifier.Verify(bytes);
                        var offlineClock = new IssuerClock { Now = payload.ValidFrom };
                        var offline = new PvadLicenseAuthority(verifier, offlineClock, payload.ValidFrom.AddYears(-1));
                        offline.Load(new MemoryStream(bytes));
                        offline.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf");
                        offlineClock.Now = payload.ValidUntil!.Value.AddTicks(-1);
                        offline.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf");
                        offlineClock.Now = payload.ValidUntil.Value;
                        var denied = false;
                        try { offline.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf"); }
                        catch (PvadLicenseException error) when (error.Code == "PVADLIC009") { denied = true; }
                        if (!denied || payload.GracePeriodDays != 0 || payload.ValidUntil != payload.ValidFrom.AddMonths(3)) throw new InvalidOperationException();
                        result = new { validSignature = true, offlineAdmission = true, expiryBlocked = true, zeroGrace = true,
                            accountType = payload.AccountScope!.AccountType, licenseId = payload.LicenseId }; break;
                    case "summary":
                        result = new
                        {
                            customers = await db.Users.CountAsync(user => user.Email != null && user.Email.EndsWith("@account-live.test")),
                            templates = await db.DesignerTemplates.CountAsync(), published = await db.DesignerTemplates.CountAsync(value => value.PublishedVersionId != null),
                            versions = await db.DesignerTemplateVersions.CountAsync(), codeWorkspaces = await db.DesignerCodeWorkspaces.CountAsync(),
                            authoredCodeWorkspaces = await db.DesignerCodeWorkspaces.CountAsync(value => value.CSharpPdfDraft.Contains("account-live-authored-edit")),
                            licenses = await db.NativeLibraryLicenses.CountAsync(), renewals = await db.AuditEvents.CountAsync(value => value.Action == "native-licenses.renew"),
                            completedRegistrations = await db.NativeLicenseRegistrationJobs.CountAsync(value => value.Status == NativeLicenseRegistrationStatus.Completed),
                        }; break;
                    default: throw new InvalidOperationException("Unknown isolated fixture control.");
                }
                Reply(command.Id, result);
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Isolated account browser host failed: " + error.GetType().Name);
            Environment.ExitCode = 1;
        }
    }

    private static async Task SeedAsync(IServiceProvider services, Guid actorId)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<PvadIdentityRole>>();
        foreach (var definition in PvadRoles.Permissions)
        {
            var role = new PvadIdentityRole { Name = definition.Key, IsSystemRole = true };
            if (!(await roles.CreateAsync(role)).Succeeded) throw new InvalidOperationException();
            foreach (var permission in definition.Value)
                if (!(await roles.AddClaimAsync(role, new Claim(PvadClaimTypes.Permission, permission))).Succeeded) throw new InvalidOperationException();
        }
        var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        db.Users.Add(new PvadIdentityUser { Id = actorId, UserName = "synthetic-manufacturer", Email = "manufacturer@fixture.test",
            DisplayName = "Synthetic manufacturer", EmailConfirmed = true });
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = actorId, RoleId = (await roles.FindByNameAsync(PvadRoles.SystemAdministrator))!.Id });
        foreach (var type in new[] { LegalDocumentType.TermsAndConditions, LegalDocumentType.PrivacyNotice })
        {
            var document = new LegalDocument { Type = type, Key = type == LegalDocumentType.PrivacyNotice ? "privacy" : "terms",
                DisplayName = "Synthetic account workflow policy", CreatedByUserId = actorId };
            const string markdown = "# Synthetic local test policy\n\nTest fixture only. No customer legal terms are published.";
            db.AddRange(document, new LegalDocumentVersion { LegalDocumentId = document.Id, Version = "synthetic-workflow-v1", Locale = "en",
                Audience = LegalDocumentAudience.All, Status = LegalDocumentStatus.Published, IsAuthoritative = true, RequiresAcceptance = true,
                SourceMarkdown = markdown, RenderedHtml = PvadLegalDocumentService.RenderSafeHtml(markdown), ContentHash = PvadLegalDocumentService.ComputeHash(markdown),
                CreatedByUserId = actorId, PublishedByUserId = actorId, PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-1), EffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        }
        await db.SaveChangesAsync();
    }

    private sealed class IssuerClock : TimeProvider { public DateTimeOffset? Now { get; set; } public override DateTimeOffset GetUtcNow() => Now ?? DateTimeOffset.UtcNow; }
    private sealed record Boot(string RunId, string WorkDirectory, string AccountUrl, string DesignerUrl, string PrivateKeyPath, string WorkerPath);
    private sealed record Command(string Id, string Action, string? Email, Guid? LicenseId, string? Path);
}
