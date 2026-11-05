using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Application.Legal;
using PVAD.WebApi.Security;

try
{
    if (args.Length != 0) throw new InvalidOperationException("No command-line inputs are accepted.");
    var buffer = new char[64 * 1024 + 1];
    var length = await Console.In.ReadBlockAsync(buffer.AsMemory());
    if (length > 64 * 1024) throw new InvalidOperationException("Synthetic control input is too large.");
    var input = new string(buffer, 0, length);
    var request = JsonSerializer.Deserialize<ControlRequest>(input) ?? throw new InvalidOperationException("Missing control input.");
    if (!Guid.TryParseExact(request.RunId, "N", out _)) throw new InvalidOperationException("A synthetic run ID is required.");
    var owner = Path.Combine(request.WorkDirectory, "owner.txt");
    if (await File.ReadAllTextAsync(owner) != request.RunId) throw new InvalidOperationException("Workspace ownership mismatch.");
    var connection = new NpgsqlConnectionStringBuilder(request.ConnectionString);
    if (connection.Host != "127.0.0.1" || connection.Port == 5432 ||
        connection.Database != "pvad_https_" + request.RunId && connection.Database != "pvad_https_restore_" + request.RunId)
        throw new InvalidOperationException("Only the isolated loopback database is supported.");
    var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders());
    services.AddDbContext<PvadDbContext>(options => options.UseNpgsql(request.ConnectionString));
    services.AddIdentityCore<PvadIdentityUser>().AddRoles<PvadIdentityRole>().AddEntityFrameworkStores<PvadDbContext>();
    await using var provider = services.BuildServiceProvider();
    var database = provider.GetRequiredService<PvadDbContext>();
    object result = request.Action switch
    {
        "seed" => await SeedAsync(provider, database, request.Password),
        "legacy-ticket" => await LegacyTicketAsync(database, request),
        "create-protected-data" => await CreateProtectedDataAsync(request),
        "protected-data" => await ProtectedDataAsync(request),
        _ => throw new InvalidOperationException("Unknown synthetic control action."),
    };
    Console.WriteLine(JsonSerializer.Serialize(result));
}
catch (FixtureFailureException exception)
{
    Console.Error.WriteLine("Fixture failure: " + exception.Code);
    Environment.ExitCode = 1;
}
catch
{
    Console.Error.WriteLine("Isolated identity fixture operation failed; no input or credential is logged.");
    Environment.ExitCode = 1;
}

static async Task<object> SeedAsync(IServiceProvider services, PvadDbContext database, string? password)
{
    if (string.IsNullOrWhiteSpace(password) || password.Length < 32 || await database.Users.AnyAsync())
        throw new InvalidOperationException("An empty synthetic store and fresh strong password are required.");
    var roles = services.GetRequiredService<RoleManager<PvadIdentityRole>>();
    foreach (var definition in PvadRoles.Permissions)
    {
        var role = new PvadIdentityRole { Name = definition.Key, IsSystemRole = true };
        Ensure(await roles.CreateAsync(role));
        foreach (var permission in definition.Value)
            Ensure(await roles.AddClaimAsync(role, new Claim(PvadClaimTypes.Permission, permission)));
    }
    var manager = services.GetRequiredService<UserManager<PvadIdentityUser>>();
    var users = new List<(PvadIdentityUser User, Organization Organization)>();
    foreach (var (email, role) in new[] { ("operator@https.test", PvadRoles.SystemAdministrator), ("editor@https.test", PvadRoles.Editor) })
    {
        var user = new PvadIdentityUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Synthetic HTTPS " + role };
        Ensure(await manager.CreateAsync(user, password));
        if (role == PvadRoles.SystemAdministrator) Ensure(await manager.AddToRoleAsync(user, role));
        var organization = new Organization { Name = "Synthetic " + role, Slug = "https-" + (role == PvadRoles.Editor ? "editor" : "operator") };
        var membership = new OrganizationMembership { UserId = user.Id, OrganizationId = organization.Id };
        var subscription = new OrganizationSubscription { OrganizationId = organization.Id, Edition = SubscriptionEdition.Premium,
            AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active, BillingPeriod = SubscriptionBillingPeriod.Monthly };
        database.AddRange(organization, membership, subscription);
        database.OrganizationMembershipRoles.Add(new OrganizationMembershipRole { OrganizationMembershipId = membership.Id,
            RoleId = (await roles.FindByNameAsync(role))!.Id });
        database.SubscriptionEntitlements.Add(new SubscriptionEntitlement { SubscriptionId = subscription.Id, Capability = "designer",
            Enabled = true, Source = EntitlementSource.EditionDefault });
        users.Add((user, organization));
    }
    await database.SaveChangesAsync();
    foreach (var type in new[] { LegalDocumentType.TermsAndConditions, LegalDocumentType.PrivacyNotice })
    {
        var document = new LegalDocument { Type = type, Key = type == LegalDocumentType.PrivacyNotice ? "privacy" : "terms",
            DisplayName = "Synthetic HTTPS fixture " + type, CreatedByUserId = users[0].User.Id };
        const string markdown = "# Synthetic test policy\n\nIsolated test data only, not a published customer agreement.";
        var version = new LegalDocumentVersion { LegalDocumentId = document.Id, Version = "https-fixture-1", Locale = "en",
            SourceMarkdown = markdown, RenderedHtml = PvadLegalDocumentService.RenderSafeHtml(markdown),
            ContentHash = PvadLegalDocumentService.ComputeHash(markdown), Status = LegalDocumentStatus.Published,
            IsAuthoritative = true, RequiresAcceptance = type == LegalDocumentType.TermsAndConditions,
            CreatedByUserId = users[0].User.Id, ApprovedByUserId = users[1].User.Id, PublishedByUserId = users[1].User.Id,
            ApprovedAt = DateTimeOffset.UtcNow.AddMinutes(-2), PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-1), EffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        database.AddRange(document, version);
        database.LegalPublicationApprovals.Add(new LegalPublicationApproval { LegalDocumentVersionId = version.Id,
            ReviewerUserId = users[1].User.Id, Decision = LegalApprovalDecision.Approved, Comment = "Synthetic fixture, not a governance workflow test." });
        foreach (var (user, organization) in users)
            database.LegalAcceptanceEvents.Add(new LegalAcceptanceEvent { UserId = user.Id, OrganizationId = organization.Id,
                LegalDocumentVersionId = version.Id, DocumentType = type.ToString(), Decision = type == LegalDocumentType.PrivacyNotice ? "acknowledged" : "accepted",
                ContentHash = version.ContentHash, Locale = "en", Source = "isolated-https-fixture" });
    }
    await database.SaveChangesAsync();
    return new { userIds = users.Select(value => new { email = value.User.Email, id = value.User.Id, organizationId = value.Organization.Id }),
        cookies = new { account = PvadCookieNames.Session(true), designer = PvadCookieNames.Designer(true), csrf = PvadCookieNames.Antiforgery(true),
            oldAccount = PvadCookieNames.LegacySession(true), oldDesigner = PvadCookieNames.LegacyDesigner(true) } };
}

static IDataProtectionProvider Keys(ControlRequest request)
{
    var directory = Path.GetFullPath(request.KeysDirectory ?? throw new InvalidOperationException("Missing key directory."));
    if (!directory.StartsWith(Path.GetFullPath(request.WorkDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !Directory.Exists(directory))
        throw new InvalidOperationException("The keyring must be in the owned temporary workspace.");
    return DataProtectionProvider.Create(new DirectoryInfo(directory), options => options.SetApplicationName(PvadDataProtection.ApplicationName));
}

static async Task<object> LegacyTicketAsync(PvadDbContext database, ControlRequest request)
{
    var scheme = request.Surface == "account" ? IdentityConstants.ApplicationScheme
        : request.Surface == "designer" ? PvadAuthenticationSchemes.DesignerCookie
        : throw new InvalidOperationException("Unsupported ticket surface.");
    var keys = Keys(request);
    TicketDataFormat Format(string value) => new(keys.CreateProtector("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", value, "v2"));
    var ticket = Format(scheme).Unprotect(request.Cookie) ?? throw new FixtureFailureException("ticket-decode");
    if (ticket.Properties.ExpiresUtc is not { } expiry || expiry <= DateTimeOffset.UtcNow)
        throw new FixtureFailureException("ticket-expired");
    var id = Guid.Parse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var sessionId = Guid.Parse(ticket.Principal.FindFirstValue(PvadClaimTypes.Session)!);
    var user = await database.Users.SingleAsync(value => value.Id == id && value.IsActive && value.EmailConfirmed);
    if (!await database.UserSessions.AnyAsync(value => value.Id == sessionId && value.UserId == id && value.RevokedAt == null && value.ExpiresAt > DateTimeOffset.UtcNow))
        throw new FixtureFailureException("session-inactive");
    if (ticket.Principal.FindFirstValue("AspNet.Identity.SecurityStamp") != user.SecurityStamp)
        throw new FixtureFailureException("stamp-mismatch");
    var oldScheme = request.Surface == "designer" ? PvadAuthenticationSchemes.LegacyDesignerCookie : scheme;
    var old = Format(oldScheme).Protect(new AuthenticationTicket(ticket.Principal, ticket.Properties, oldScheme));
    return new { name = request.Surface == "designer" ? PvadCookieNames.LegacyDesigner(true) : PvadCookieNames.LegacySession(true), value = old };
}

static async Task<object> CreateProtectedDataAsync(ControlRequest request)
{
    var keys = Keys(request);
    const string value = "Synthetic protected consumer payload";
    var oldMail = keys.CreateProtector(PvadDataProtection.LegacyMailPayloadPurpose).Protect(value);
    var oldCache = keys.CreateProtector(PvadDataProtection.LegacyRevocationEvidencePurpose).Protect(value);
    await File.WriteAllTextAsync(Path.Combine(request.WorkDirectory, "protected-consumers.json"),
        JsonSerializer.Serialize(new ProtectedConsumers(oldMail, oldCache)));
    return new { savedBeforeBackup = true };
}

static async Task<object> ProtectedDataAsync(ControlRequest request)
{
    var keys = Keys(request);
    var payload = JsonSerializer.Deserialize<ProtectedConsumers>(
        await File.ReadAllTextAsync(Path.Combine(request.WorkDirectory, "protected-consumers.json")))!;
    const string value = "Synthetic protected consumer payload";
    if (PvadDataProtection.MailPayload(keys).Unprotect(payload.Mail) != value || PvadDataProtection.RevocationEvidence(keys).Unprotect(payload.Cache) != value)
        throw new InvalidOperationException("Retained protected-data consumers failed.");
    return new { mailLegacyReadable = true, cacheLegacyReadable = true };
}

static void Ensure(IdentityResult result)
{
    if (!result.Succeeded) throw new InvalidOperationException("Synthetic Identity setup failed.");
}

sealed record ControlRequest(string Action, string RunId, string WorkDirectory, string ConnectionString,
    string? Password, string? KeysDirectory, string? Surface, string? Cookie);

sealed record ProtectedConsumers(string Mail, string Cache);

sealed class FixtureFailureException(string code) : Exception
{
    public string Code { get; } = code;
}
