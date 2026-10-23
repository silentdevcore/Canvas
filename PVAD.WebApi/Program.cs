using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization.Policy;
using OpenIddict.Validation.AspNetCore;
using System.Threading.RateLimiting;
using PVAD.Core.Contracts;
using PVAD.FileImporter;
using PVAD.FileImporter.ImageAnalysis;
using PVAD.FileImporter.ImageOcr;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.Pdf;
using PVAD.WebApi.Application.Identity;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Application.Organizations;
using PVAD.WebApi.Application.Subscriptions;
using PVAD.WebApi.Application.Legal;
using PVAD.WebApi.Application.Compliance;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Observability;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Mail;
using PVAD.WebApi.Services.Entitlements;
using PVAD.WebApi.Services.Licensing;
using PVAD.WebApi.Services.Jobs;
using PVAD.WebApi.Services.Storage;
using PvadConverters = PVAD.Infrastructure.Converters;
using PvadSpreadsheet = PVAD.Infrastructure.Spreadsheet;
using PvadWord = PVAD.Infrastructure.Word;

if (args is ["--health-check"])
{
    using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await healthClient.GetAsync("http://127.0.0.1:8080/health/live");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception exception) when (
        exception is HttpRequestException or TaskCanceledException)
    {
        Environment.ExitCode = 1;
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);
_ = PVAD.Infrastructure.Persistence.Configuration.PvadEnvironment.Read("PVAD_BUILD_COMMIT");
_ = PVAD.Infrastructure.Persistence.Configuration.PvadEnvironment.Read("PVAD_BUILD_TIME");
builder.AddPvadObservability();

// Add services to the container.
builder.Services.AddControllers(options => options.Filters.Add<PvadProblemDetailsResultFilter>())
    .AddJsonOptions(options =>
        PvadDiagnosticJsonContract.Configure(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options =>
    PvadDiagnosticJsonContract.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        // MVC adds caller extensions after creating the problem. Its result filter
        // completes diagnostics later, avoiding duplicate extension keys here.
        if (context.Exception is not null || context.ProblemDetails.Extensions.ContainsKey("code"))
            PvadApiProblems.Complete(
                context.HttpContext,
                context.ProblemDetails,
                context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError);
    };
});
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<PVAD.WebApi.Infrastructure.OpenApi.PvadEInvoiceOpenApiTransformer>();
    options.AddDocumentTransformer<PVAD.WebApi.Infrastructure.OpenApi.PvadDesignerProjectsOpenApiTransformer>();
    options.AddDocumentTransformer<PVAD.WebApi.Infrastructure.OpenApi.PvadChartOpenApiTransformer>();
});
builder.Services.AddPvadPersistence(serviceProvider =>
    serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("PvadDatabase")
        ?? throw new InvalidOperationException("Connection string 'PvadDatabase' is required."));
builder.Services.AddPvadOAuthServer(builder.Configuration, builder.Environment);
var requireSecureCookies = !builder.Environment.IsDevelopment();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = PvadAuthenticationSchemes.Combined;
        options.DefaultChallengeScheme = PvadAuthenticationSchemes.Combined;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddPolicyScheme(PvadAuthenticationSchemes.Combined, PvadAuthenticationSchemes.Combined, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (context.Request.Headers.ContainsKey("X-PVAD-API-Key") ||
                PvadApiKeySecret.IsBearer(authorization))
                return PvadAuthenticationSchemes.ApiKey;
            if (PvadMcpDelegationAuthenticationHandler.HasDelegationPrefix(authorization))
                return PvadAuthenticationSchemes.McpDelegation;
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            return string.Equals(
                    context.Request.Headers["X-PVAD-Application"].ToString(),
                    "designer",
                    StringComparison.OrdinalIgnoreCase)
                    ? PvadAuthenticationSchemes.DesignerCookie
                    : IdentityConstants.ApplicationScheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, PvadApiKeyAuthenticationHandler>(
        PvadAuthenticationSchemes.ApiKey, _ => { })
    .AddScheme<AuthenticationSchemeOptions, PvadMcpDelegationAuthenticationHandler>(
        PvadAuthenticationSchemes.McpDelegation, _ => { })
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = PvadCookieNames.Session(requireSecureCookies);
        options.Cookie.Path = "/";
        options.CookieManager = new PvadMigratingCookieManager(PvadCookieNames.LegacySession(requireSecureCookies));
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = requireSecureCookies
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(PvadCookieAuthenticationEvents);
    })
    .AddCookie(PvadAuthenticationSchemes.DesignerCookie, options =>
    {
        options.Cookie.Name = PvadCookieNames.Designer(requireSecureCookies);
        options.Cookie.Path = "/";
        options.CookieManager = new PvadMigratingCookieManager(PvadCookieNames.LegacyDesigner(requireSecureCookies));
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = requireSecureCookies
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(PvadCookieAuthenticationEvents);
    });
builder.Services.AddScoped<PvadCookieAuthenticationEvents>();
builder.Services.AddScoped<IClaimsTransformation, PvadPermissionClaimsTransformation>();
builder.Services.AddScoped<PVAD.Importer.Trust.IPdfTrustInspector, PVAD.Importer.Trust.PdfTrustInspector>();
builder.Services.AddScoped<PVAD.Importer.Redaction.IPdfRedactionPreflight, PVAD.Importer.Redaction.PdfRedactionPreflight>();
builder.Services.AddScoped<PVAD.Importer.Trust.IPdfA2bConverter, PVAD.Importer.Trust.PdfA2bConverter>();
builder.Services.AddScoped<PVAD.Importer.EInvoicing.IPdfEInvoiceAttacher, PVAD.Importer.EInvoicing.PdfEInvoiceAttacher>();
builder.Services.AddOptions<PVAD.WebApi.Services.Pdf.PvadEInvoiceEvidenceOptions>()
    .Bind(builder.Configuration.GetSection(PVAD.WebApi.Services.Pdf.PvadEInvoiceEvidenceOptions.SectionName))
    .Validate(
        options => options.RetentionDays is >= 1 and <= 36_500 &&
                   options.MaximumPageSize is >= 1 and <= 1_000 &&
                   options.CleanupIntervalMinutes is >= 1 and <= 1_440 &&
                   options.CleanupBatchSize is >= 1 and <= 10_000,
        "Electronic-invoice evidence retention configuration is invalid.")
    .ValidateOnStart();
builder.Services.AddScoped<PVAD.WebApi.Services.Pdf.IPvadEInvoiceValidationEvidenceService,
    PVAD.WebApi.Services.Pdf.PvadEInvoiceValidationEvidenceService>();
builder.Services.AddHostedService<PVAD.WebApi.Services.Pdf.PvadEInvoiceValidationEvidenceRetentionWorker>();
builder.Services.Configure<PVAD.WebApi.Services.Pdf.PvadEInvoiceValidationOptions>(
    builder.Configuration.GetSection(PVAD.WebApi.Services.Pdf.PvadEInvoiceValidationOptions.SectionName));
builder.Services.AddSingleton<PVAD.WebApi.Services.Pdf.IPvadEInvoiceValidationService,
    PVAD.WebApi.Services.Pdf.PvadEInvoiceValidationService>();
builder.Services.AddScoped<PVAD.Importer.Redaction.IPdfRedactionApplier, PVAD.Importer.Redaction.PdfRedactionApplier>();
builder.Services.Configure<PVAD.WebApi.Services.Pdf.PvadRedactionAuditKeyOptions>(
    builder.Configuration.GetSection(PVAD.WebApi.Services.Pdf.PvadRedactionAuditKeyOptions.SectionName));
builder.Services.AddSingleton<
    PVAD.WebApi.Services.Pdf.IPvadRedactionAuditKeyProvider,
    PVAD.WebApi.Services.Pdf.PvadRedactionAuditKeyProvider>();
builder.Services.AddScoped<PVAD.WebApi.Services.Pdf.PdfViewerRedactionRegionMapper>();
builder.Services.AddScoped<PVAD.Importer.Signing.IPdfPadesSigningService, PVAD.Importer.Signing.PdfPadesSigningService>();
builder.Services.AddScoped<PVAD.Importer.Trust.IPdfTrustPostProcessingPipeline,
    PVAD.Importer.Trust.PdfTrustPostProcessingPipeline>();
builder.Services.AddScoped<PVAD.Importer.Signing.IPdfDocumentTimestampService,
    PVAD.Importer.Signing.PdfDocumentTimestampService>();
builder.Services.AddOptions<PVAD.WebApi.Services.Pdf.PvadPdfTimestampAuthorityOptions>()
    .Bind(builder.Configuration.GetSection(PVAD.WebApi.Services.Pdf.PvadPdfTimestampAuthorityOptions.SectionName))
    .Validate(
        options => string.IsNullOrWhiteSpace(options.Endpoint) ||
                   Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) &&
                   endpoint.Scheme == Uri.UriSchemeHttps,
        "The PDF timestamp authority endpoint must be an absolute HTTPS URI.")
    .Validate(
        options => options.MaximumResponseBytes is >= 1024 and <= 16 * 1024 * 1024,
        "The PDF timestamp response limit must be between 1 KiB and 16 MiB.")
    .ValidateOnStart();
builder.Services.AddHttpClient(PVAD.WebApi.Services.Pdf.PvadPdfTimestampAuthorityFactory.HttpClientName, client =>
    client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<PVAD.WebApi.Services.Pdf.IPvadPdfTimestampAuthorityFactory,
    PVAD.WebApi.Services.Pdf.PvadPdfTimestampAuthorityFactory>();
builder.Services.AddOptions<PVAD.WebApi.Services.Pdf.PvadPdfRevocationEvidenceOptions>()
    .Bind(builder.Configuration.GetSection(PVAD.WebApi.Services.Pdf.PvadPdfRevocationEvidenceOptions.SectionName))
    .Validate(
        options => options.AllowedHosts.All(host => !string.IsNullOrWhiteSpace(host)) &&
                   options.CrlPaths.All(path => !string.IsNullOrWhiteSpace(path)) &&
                   options.OcspResponsePaths.All(path => !string.IsNullOrWhiteSpace(path)) &&
                   !string.IsNullOrWhiteSpace(options.CacheDirectory),
        "PDF revocation evidence hosts, CRL paths, and cache directory must not be empty.")
    .Validate(
        options => options.MaximumResponseBytes is >= 1024 and <= 16 * 1024 * 1024 &&
                   options.NetworkTimeoutSeconds is >= 1 and <= 60 &&
                   options.MaximumOcspAgeHours is >= 1 and <= 168,
        "PDF revocation evidence response limit or timeout is invalid.")
    .ValidateOnStart();
builder.Services.AddHttpClient(PVAD.WebApi.Services.Pdf.PvadPdfRevocationEvidenceProvider.HttpClientName, (services, client) =>
    client.Timeout = TimeSpan.FromSeconds(
        services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PVAD.WebApi.Services.Pdf.PvadPdfRevocationEvidenceOptions>>()
            .Value.NetworkTimeoutSeconds))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<PVAD.Importer.Trust.IPdfRevocationEvidenceProvider,
    PVAD.WebApi.Services.Pdf.PvadPdfRevocationEvidenceProvider>();
builder.Services.AddSingleton<PVAD.Importer.Trust.IPdfIssuerCertificateProvider>(services =>
    (PVAD.WebApi.Services.Pdf.PvadPdfRevocationEvidenceProvider)services
        .GetRequiredService<PVAD.Importer.Trust.IPdfRevocationEvidenceProvider>());
builder.Services.AddOptions<PVAD.WebApi.Services.Pdf.PvadPdfTrustPolicyOptions>()
    .Bind(builder.Configuration.GetSection(PVAD.WebApi.Services.Pdf.PvadPdfTrustPolicyOptions.SectionName))
    .Validate(
        options => options.CustomTrustAnchorPaths.All(path => !string.IsNullOrWhiteSpace(path)) &&
                   options.IntermediateCertificatePaths.All(path => !string.IsNullOrWhiteSpace(path)),
        "PDF trust-store certificate paths must not be empty.")
    .Validate(
        options => Enum.IsDefined(options.RevocationMode) &&
                   Enum.IsDefined(options.RevocationFlag) &&
                   options.NetworkTimeoutSeconds is >= 1 and <= 60,
        "PDF revocation settings or network timeout are invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton<PVAD.WebApi.Services.Pdf.IPvadPdfTrustPolicyProvider,
    PVAD.WebApi.Services.Pdf.PvadPdfTrustPolicyProvider>();
builder.Services.AddOptions<PVAD.WebApi.Services.Pdf.PvadPdfTrustRuntimeOptions>()
    .Bind(builder.Configuration.GetSection(PVAD.WebApi.Services.Pdf.PvadPdfTrustRuntimeOptions.SectionName))
    .Validate(options => options.DeploymentMode is "Cloud" or "OnPremise" &&
                         options.KeyProviderAdapter is "Pkcs12Upload" or "Hsm" or "Kms" &&
                         options.AuditRetentionDays is >= 30 and <= 36500,
        "PDF Trust deployment mode, key-provider adapter, or audit retention is invalid.")
    .ValidateOnStart();
builder.Services.AddHostedService<PVAD.WebApi.Services.Pdf.PvadPdfTrustStartupGate>();
builder.Services.AddSingleton<PVAD.Importer.Signing.IPdfLongTermValidationMaterialProvider,
    PVAD.WebApi.Services.Pdf.PvadPdfLongTermValidationMaterialProvider>();
builder.Services.AddScoped<PVAD.WebApi.Services.Pdf.IPvadPdfTrustAuditWriter,
    PVAD.WebApi.Services.Pdf.PvadPdfTrustAuditWriter>();
builder.Services.AddScoped<PvadSessionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<PvadStepUpOptions>()
    .Bind(builder.Configuration.GetSection(PvadStepUpOptions.SectionName))
    .Validate(options => options.MaximumAuthenticationAgeMinutes is >= 1 and <= 60,
        "Step-up authentication age must be between 1 and 60 minutes.")
    .ValidateOnStart();
builder.Services.AddScoped<IAuthorizationHandler, PvadRecentAuthenticationHandler>();
builder.Services.AddOptions<PvadPasswordSecurityOptions>()
    .Bind(builder.Configuration.GetSection(PvadPasswordSecurityOptions.SectionName))
    .Validate(
        options => options.BreachedPasswordSha256.All(value =>
            value.Length == 64 && value.All(Uri.IsHexDigit)),
        "Every breached-password fingerprint must be a SHA-256 hexadecimal value.")
    .ValidateOnStart();
builder.Services.AddOptions<PvadAdminSecurityOptions>()
    .Bind(builder.Configuration.GetSection(PvadAdminSecurityOptions.SectionName));
builder.Services.AddScoped<PvadSystemOperatorAccess>();
builder.Services.AddOptions<PvadAccountClosureOptions>()
    .Bind(builder.Configuration.GetSection(PvadAccountClosureOptions.SectionName));
builder.Services.AddOptions<PvadRegistrationOptions>()
    .Bind(builder.Configuration.GetSection(PvadRegistrationOptions.SectionName))
    .Validate(options =>
            !string.IsNullOrWhiteSpace(options.TermsVersion) &&
            options.TermsVersion.Length <= 64 &&
            !string.IsNullOrWhiteSpace(options.PrivacyVersion) &&
            options.PrivacyVersion.Length <= 64 &&
            options.AllowedReturnOrigins.Length > 0,
        "Registration Terms, Privacy versions, and at least one return origin are required.")
    .ValidateOnStart();
builder.Services.AddOptions<PvadDesignerAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(PvadDesignerAuthenticationOptions.SectionName))
    .Validate(options => options.AllowedOrigins.Length > 0,
        "At least one Designer origin must be configured.")
    .ValidateOnStart();
builder.Services.AddOptions<PvadDesignerTemplateOptions>()
    .Bind(builder.Configuration.GetSection(PvadDesignerTemplateOptions.SectionName))
    .Validate(options =>
            options.MaximumDesignJsonBytes is >= 1024 and <= 100 * 1024 * 1024 &&
            options.MaximumPageSize is >= 1 and <= 500,
        "Designer template limits are outside the supported range.")
    .ValidateOnStart();
builder.Services.AddOptions<PvadStorageOptions>()
    .Bind(builder.Configuration.GetSection(PvadStorageOptions.SectionName))
    .Validate(options =>
            string.Equals(options.Provider, "FileSystem", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(options.RootPath) &&
            options.MaximumObjectBytes is >= 1024 and <= 5L * 1024 * 1024 * 1024 &&
            options.MaximumDesignerAssetBytes is >= 1024 and <= 100L * 1024 * 1024 &&
            options.MaximumDesignerAssetBytes <= options.MaximumObjectBytes,
        "Storage provider, root path, or object-size limit is invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<PvadJobOptions>()
    .Bind(builder.Configuration.GetSection(PvadJobOptions.SectionName))
    .Validate(options =>
            options.PollIntervalSeconds is >= 1 and <= 300 &&
            options.LeaseMinutes is >= 1 and <= 1440 &&
            options.MaximumAttempts is >= 1 and <= 20 &&
            options.ResultRetentionDays is >= 1 and <= 3650 &&
            options.TransientRetentionHours is >= 1 and <= 24 &&
            options.PdfTrustInspectionRetentionHours is >= 1 and <= 24 &&
            options.TerminalMetadataRetentionDays is >= 1 and <= 3650 &&
            options.CleanupIntervalMinutes is >= 1 and <= 1440 &&
            options.CleanupBatchSize is >= 1 and <= 10000 &&
            options.MetricsIntervalSeconds is >= 5 and <= 300,
        "Background job timing or retry settings are invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, PvadAuthorizationMiddlewareResultHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var problem = PvadApiProblems.Create(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            detail: "The request rate limit was exceeded. Retry after the current limit window.");
        await context.HttpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("identity-action", context => RateLimitPartition.GetFixedWindowLimiter(
        $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{context.Request.Path}",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        }));
    options.AddPolicy("invitations", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(PvadClaimTypes.ActiveOrganization)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("account-service-accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(PvadClaimTypes.ActiveOrganization)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("registration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("oauth-client-registration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("browser-telemetry", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("designer-code", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(PvadClaimTypes.ActiveOrganization)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("designer-symbols", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(PvadClaimTypes.ActiveOrganization)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 240,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPvadTenantContext, PvadTenantContext>();
builder.Services.AddScoped<IPvadEntitlementService, PvadEntitlementService>();
builder.Services.AddScoped<IPvadUsageService, PvadUsageService>();
builder.Services.AddSingleton<PvadDesignerProductMetadata>();
builder.Services.AddScoped<IPvadDesignerFeatureGate, PvadDesignerFeatureGate>();
builder.Services.AddScoped<IPvadDesignerProjectAuditWriter, PvadDesignerProjectAuditWriter>();
builder.Services.AddOptions<PvadCodeWorkerOptions>()
    .Bind(builder.Configuration.GetSection("CodeWorker"))
    .Validate(options => options.TimeoutSeconds is >= 1 and <= 15 &&
                         options.MaximumSourceBytes is > 0 and <= PvadCodeLimits.MaximumSourceBytes &&
                         options.MaximumOutputBytes is > 0 and <= 50_000_000,
        "Code worker limits are invalid.")
    .Validate(options => builder.Environment.IsDevelopment() || !options.Enabled || options.Hardened,
        "Production code execution requires CodeWorker:Hardened=true and an isolated deployment.")
    .ValidateOnStart();
builder.Services.AddScoped<IPvadCodeWorkerClient, PvadCodeWorkerClient>();
builder.Services.AddScoped<IPvadCodeConversionService, PvadCodeConversionService>();
builder.Services.AddOptions<PvadProductAccessOptions>()
    .Bind(builder.Configuration.GetSection("ProductAccess"));
builder.Services.AddOptions<PvadLicensingOptions>()
    .Bind(builder.Configuration.GetSection("Licensing"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.KeyId) &&
                         !string.IsNullOrWhiteSpace(options.PrivateKeyPath) &&
                         !string.IsNullOrWhiteSpace(options.PublicKeyPath) &&
                         options.MetricsIntervalSeconds is >= 15 and <= 3600,
        "Licensing key paths and a metrics interval between 15 and 3600 seconds are required.")
    .ValidateOnStart();
builder.Services.AddSingleton<PvadLicenseSigningService>();
builder.Services.AddSingleton<IPvadLicenseSigningService>(
    services => services.GetRequiredService<PvadLicenseSigningService>());
builder.Services.AddSingleton<IPvadLicenseSignatureVerifier>(
    services => services.GetRequiredService<PvadLicenseSigningService>());
builder.Services.AddSingleton<PvadOfflineLicenseValidator>();
var dataProtectionKeysDirectory = builder.Configuration["DataProtection:KeysDirectory"]
    ?? Path.Combine("App_Data", "data-protection-keys");
var dataProtectionKeysPath = Path.IsPathRooted(dataProtectionKeysDirectory)
    ? dataProtectionKeysDirectory
    : Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeysDirectory);
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName(PvadDataProtection.ApplicationName)
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddOptions<PvadMailOptions>()
    .Bind(builder.Configuration.GetSection("Mail"))
    .Validate(options => new[] { "Development", "Smtp", "Disabled" }.Contains(
        options.Transport,
        StringComparer.OrdinalIgnoreCase), "Mail transport must be Development, Smtp, or Disabled.")
    .Validate(options => !string.Equals(options.Transport, "Smtp", StringComparison.OrdinalIgnoreCase) ||
                         (!string.IsNullOrWhiteSpace(options.SmtpHost) &&
                          options.SmtpPort is > 0 and <= 65535 &&
                          options.SmtpTimeoutSeconds is > 0 and <= 300),
        "SMTP host, port, or timeout is invalid.")
    .Validate(options =>
            options.DeliveredRetentionDays is > 0 and <= 3650 &&
            options.CancelledRetentionDays is > 0 and <= 3650 &&
            options.DeadLetterRetentionDays is > 0 and <= 3650 &&
            options.RetentionCleanupIntervalMinutes is > 0 and <= 1440 &&
            options.RetentionBatchSize is > 0 and <= 10000,
        "Mail retention settings are invalid.")
    .Validate(options => new[]
        {
            options.AdminBaseUrl,
            options.AccountBaseUrl,
            options.CompanyBaseUrl,
            options.DesignerBaseUrl,
            options.SupportUrl,
        }.All(value =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))),
        "Mail public URLs must be absolute HTTP or HTTPS URLs.")
    .ValidateOnStart();
builder.Services.AddScoped<IdentityActionTokenService>();
builder.Services.AddScoped<TrialActivationService>();
builder.Services.AddScoped<RegistrationLegalPolicyService>();
builder.Services.AddScoped<CustomerRegistrationService>();
builder.Services.AddScoped<DesignerAuthorizationCodeService>();
builder.Services.AddScoped<OrganizationMembershipService>();
builder.Services.AddScoped<SubscriptionQueryService>();
builder.Services.AddScoped<PvadLegalDocumentService>();
builder.Services.AddSingleton<PvadLegalContentCatalog>();
builder.Services.AddScoped<AccountLegalObligationService>();
builder.Services.Configure<PvadConsumerCheckoutOptions>(
    builder.Configuration.GetSection(PvadConsumerCheckoutOptions.SectionName));
builder.Services.AddScoped<PvadConsumerCheckoutLegalGate>();
builder.Services.AddSingleton<PvadCommerceReadinessCatalog>();
builder.Services.AddScoped<PvadPaidCheckoutReadinessGate>();
builder.Services.AddSingleton<PVAD.WebApi.Application.Retention.PvadRetentionPolicyCatalog>();
builder.Services.AddScoped<PVAD.WebApi.Application.Retention.PvadRetentionLegalHoldService>();
builder.Services.AddScoped<PVAD.WebApi.Application.Retention.PvadRetentionGovernanceService>();
builder.Services.AddHostedService<PVAD.WebApi.Application.Retention.PvadRetentionStartupGate>();
builder.Services.AddScoped<IPvadMailQueue, PvadMailQueue>();
builder.Services.AddScoped<PvadMailProcessor>();
builder.Services.AddScoped<PvadMailRetentionService>();
builder.Services.AddScoped<OrganizationNotificationService>();
builder.Services.AddSingleton<PvadMailTemplateRenderer>();
builder.Services.AddSingleton<DevelopmentMailTransport>();
builder.Services.AddSingleton<SmtpMailTransport>();
builder.Services.AddSingleton<DisabledMailTransport>();
builder.Services.AddSingleton<IPvadMailTransport>(services =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PvadMailOptions>>().Value;
    return options.Transport.ToLowerInvariant() switch
    {
        "smtp" => services.GetRequiredService<SmtpMailTransport>(),
        "disabled" => services.GetRequiredService<DisabledMailTransport>(),
        _ => services.GetRequiredService<DevelopmentMailTransport>(),
    };
});
builder.Services.AddScoped<TrialExpiryNotifier>();
builder.Services.AddSingleton<IPvadObjectStorage, FileSystemPvadObjectStorage>();
builder.Services.AddScoped<PvadStoredObjectService>();
builder.Services.AddScoped<IPvadJobQueue, PvadJobQueue>();
builder.Services.AddScoped<PvadJobProcessor>();
builder.Services.AddScoped<PdfTrustInspectionJobExecutor>();
builder.Services.AddScoped<PvadJobRetentionService>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<PvadMailWorker>();
    builder.Services.AddHostedService<TrialExpiryWorker>();
    builder.Services.AddHostedService<PvadJobWorker>();
    builder.Services.AddHostedService<PvadJobRetentionWorker>();
    builder.Services.AddHostedService<PvadJobMetricsPublisher>();
    builder.Services.AddHostedService<PvadLicensingMetricsPublisher>();
}
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PvadPermissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim(PvadClaimTypes.Permission, permission));
    foreach (var permission in PvadAccountPermissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim(PvadClaimTypes.Permission, permission));
    foreach (var permission in PvadDesignerPermissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim(PvadClaimTypes.Permission, permission));
    options.AddPolicy(PvadSecurityPolicies.RecentAuthentication, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new PvadRecentAuthenticationRequirement());
    });
    AddStepUpPolicy(options, PvadSecurityPolicies.OrganizationPdfSigning,
        PvadDesignerPermissions.PdfSignaturesSign);
    AddStepUpPolicy(options, PvadSecurityPolicies.AppliedPdfRedaction,
        PvadDesignerPermissions.PdfRedactionsApply);
    AddStepUpPolicy(options, PvadSecurityPolicies.PdfTrustPolicyManagement,
        PvadDesignerPermissions.PdfSignatureTrustPoliciesManage);
    AddStepUpPolicy(options, PvadSecurityPolicies.PdfKeyProviderManagement,
        PvadDesignerPermissions.PdfSignatureKeyProvidersManage);
    options.AddPolicy(PvadOAuthPolicies.TemplatesRead, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            PvadOAuthPolicies.HasScope(context.User, PvadOAuthScopes.TemplatesRead));
    });
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = PvadCookieNames.Antiforgery(requireSecureCookies);
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = requireSecureCookies
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.HeaderName = "X-PVAD-CSRF";
});
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PvadDbContext>("pxa-database", tags: ["ready"])
    .AddCheck<PvadMailHealthCheck>("pxa-mail", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddScoped<PvadSystemHealthService>();
builder.Services.AddSingleton<PvadDependencyComplianceCatalog>();
builder.Services.AddCors(options =>
{
    var corsOrigins = PvadCorsConfiguration.ResolveOrigins(builder.Configuration);
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders(PvadResponseHeaders.ExposedHeaders)
              .AllowCredentials());
});

// Register template rendering services
builder.Services.AddScoped<PVAD.Domain.Repositories.ITemplateRepository, PostgreSqlTemplateRepository>();

// Register font loader for multi-language PDF support (optional: gracefully absent if fonts dir missing)
builder.Services.AddSingleton<PdfFontLoader>(sp =>
{
    var fontsDir = builder.Configuration["Pdf:FontsDirectory"]
        ?? Path.Combine(AppContext.BaseDirectory, "fonts");
    return new PdfFontLoader(fontsDir);
});

// Register export format exporters
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.HtmlDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.XmlDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.SvgDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.CsvDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.MarkdownDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.ImageDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.JpegDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.TiffDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadConverters.OdtDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadWord.WordDocumentExporter>();
builder.Services.AddScoped<PVAD.Core.Abstractions.IDocumentExporter, PvadSpreadsheet.ExcelDocumentExporter>();

// Spreadsheet Editor SDK: workbook (SpreadsheetDto) ⇄ .xlsx round-trip (distinct from the design exporters).
builder.Services.AddScoped<PvadSpreadsheet.ExcelWorkbookExporter>();
builder.Services.AddScoped<PvadSpreadsheet.ExcelWorkbookImporter>();
builder.Services.AddScoped<PvadSpreadsheet.SpreadsheetToDesignConverter>();
builder.Services.AddScoped<PvadSpreadsheet.SpreadsheetCalculator>();
builder.Services.AddScoped<PvadSpreadsheet.SpreadsheetOperations>();
builder.Services.AddScoped<PvadSpreadsheet.XlsWorkbookIo>();
builder.Services.AddScoped<PvadSpreadsheet.SpreadsheetData>();
builder.Services.AddScoped<PvadSpreadsheet.SpreadsheetValidator>();

// Register file importers
builder.Services.AddSingleton<IRemoteImageResolver, SafeRemoteImageResolver>();
builder.Services.AddTransient<IFileImporter, PdfFileImporter>();
builder.Services.AddTransient<IFileImporter, DocxFileImporter>();
builder.Services.AddTransient<IFileImporter, PptxFileImporter>();
builder.Services.AddTransient<IFileImporter, DocFileImporter>();
builder.Services.AddTransient<IFileImporter, MarkdownFileImporter>();
builder.Services.AddTransient<IFileImporter, OdtFileImporter>();
builder.Services.AddTransient<IFileImporter, SvgFileImporter>();
builder.Services.AddTransient<IFileImporter, ImageFileImporter>();
builder.Services.AddTransient<ImageAnalysisFileImporter>();
builder.Services.AddSingleton<IOcrEngine>(sp =>
{
    var tessDataPath = builder.Configuration["Ocr:TessDataPath"];
    var nativeLibraryPath = builder.Configuration["Ocr:NativeLibraryPath"];
    var useIsolatedWorker = builder.Configuration.GetValue("Ocr:UseIsolatedWorker", true);
    IOcrEngine engine;
    if (!useIsolatedWorker)
    {
        engine = new EmbeddedTesseractOcrEngine(tessDataPath, nativeLibraryPath);
    }
    else
    {
        var workerPath = builder.Configuration["Ocr:WorkerPath"];
        engine = new ProcessIsolatedTesseractOcrEngine(
            workerPath,
            tessDataPath,
            nativeLibraryPath);
    }

    var observability = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<PvadObservabilityOptions>>().Value;
    return observability.EnableOcrFailureInjection
        ? new PvadFaultInjectingOcrEngine(engine, observability.OcrFailureInjectionCount)
        : engine;
});
builder.Services.AddTransient<ImageToPdfConverter>();

// Register migration service
builder.Services.AddSingleton<PVAD.WebApi.Services.MigrationService>();
builder.Services.AddSingleton<PVAD.WebApi.Services.ReportMigrationService>();
builder.Services.AddSingleton<PVAD.WebApi.Services.NutrientViewerDesignMigrationService>();
builder.Services.AddSingleton<PVAD.WebApi.Services.PdfViewerAnnotationStore>();
builder.Services.AddSingleton<PVAD.WebApi.Services.PdfViewerAnnotationFlatteningService>();
builder.Services.AddSingleton<PVAD.WebApi.Services.PdfViewerNativeAnnotationExtractionService>();
builder.Services.AddSingleton<PVAD.WebApi.Services.PdfViewerFormExtractionService>();

// Register use cases
builder.Services.AddScoped<PVAD.Application.UseCases.ExportDocumentUseCase>();
builder.Services.AddScoped<PVAD.Application.UseCases.CreateTemplateUseCase>();
builder.Services.AddScoped<PVAD.Application.UseCases.UpdateTemplateUseCase>();
builder.Services.AddScoped<PVAD.Application.UseCases.GetTemplateUseCase>();
builder.Services.AddScoped<PVAD.Application.UseCases.ValidateTemplateUseCase>();
builder.Services.AddScoped<PVAD.Application.UseCases.FindAndReplaceUseCase>();
builder.Services.AddScoped<PVAD.Application.UseCases.CloneTemplateUseCase>();
builder.Services.AddScoped<PVAD.Application.UseCases.ExtractPagesUseCase>();

var app = builder.Build();
PvadConfigurationAliases.Apply(builder.Configuration);

// Resolve the optional audit signer during startup. An absent configuration is valid, but
// an explicitly configured key must load successfully instead of downgrading redaction
// audit records to unsigned output on the first request.
_ = app.Services.GetRequiredService<PVAD.WebApi.Services.Pdf.IPvadRedactionAuditKeyProvider>();

await PvadOAuthBootstrapper.InitializeAsync(app);
await PvadDevelopmentIdentityBootstrapper.InitializeAsync(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseMiddleware<PvadLegacyApiMiddleware>();
app.UseRouting();
app.UseCors();
app.UseMiddleware<PvadOperationMetricsMiddleware>();
app.UseMiddleware<PvadSecurityHeaderMiddleware>();
app.UseMiddleware<PvadAntiforgeryCookieMiddleware>();
app.UseAuthentication();
app.UseMiddleware<PvadDesignerAccessMiddleware>();
app.UseMiddleware<PvadDesignerFeatureGateMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<PvadPdfTrustAvailabilityMiddleware>();
app.UseMiddleware<PvadProductAccessMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});
app.MapFallback((HttpContext context) =>
{
    var problem = PvadApiProblems.Create(
        context,
        StatusCodes.Status404NotFound,
        detail: "The requested API resource does not exist.");
    return Results.Json(
        problem,
        statusCode: StatusCodes.Status404NotFound,
        contentType: "application/problem+json");
});

static void AddStepUpPolicy(AuthorizationOptions options, string policyName, string permission)
{
    options.AddPolicy(policyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(PvadClaimTypes.Permission, permission);
        policy.AddRequirements(new PvadRecentAuthenticationRequirement());
    });
}

app.Run();

public partial class Program {}
