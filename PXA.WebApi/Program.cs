using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Threading.RateLimiting;
using PXA.FileImporter;
using PXA.FileImporter.ImageAnalysis;
using PXA.FileImporter.ImageOcr;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.Pdf;
using PXA.WebApi.Application.Identity;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Application.Organizations;
using PXA.WebApi.Application.Subscriptions;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Observability;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;
using PXA.WebApi.Services.Entitlements;
using PXA.WebApi.Services.Licensing;
using PXA.WebApi.Services.Jobs;
using PXA.WebApi.Services.Storage;
using PxaConverters = PXA.Infrastructure.Converters;
using PxaSpreadsheet = PXA.Infrastructure.Spreadsheet;
using PxaWord = PXA.Infrastructure.Word;

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
builder.AddPxaObservability();

// Add services to the container.
builder.Services.AddControllers(options => options.Filters.Add<PxaProblemDetailsResultFilter>());
builder.Services.AddOpenApi();
builder.Services.AddPxaPersistence(
    builder.Configuration.GetConnectionString("PxaDatabase")
        ?? throw new InvalidOperationException("Connection string 'PxaDatabase' is required."));
var requireSecureCookies = !builder.Environment.IsDevelopment();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = PxaAuthenticationSchemes.Combined;
        options.DefaultChallengeScheme = PxaAuthenticationSchemes.Combined;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddPolicyScheme(PxaAuthenticationSchemes.Combined, PxaAuthenticationSchemes.Combined, options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey("X-PXA-API-Key") ||
            context.Request.Headers.Authorization.ToString().StartsWith("Bearer pxa_", StringComparison.Ordinal)
                ? PxaAuthenticationSchemes.ApiKey
                : string.Equals(
                    context.Request.Headers["X-PXA-Application"].ToString(),
                    "designer",
                    StringComparison.OrdinalIgnoreCase)
                    ? PxaAuthenticationSchemes.DesignerCookie
                    : IdentityConstants.ApplicationScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, PxaApiKeyAuthenticationHandler>(
        PxaAuthenticationSchemes.ApiKey, _ => { })
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = requireSecureCookies ? "__Host-PXA.Session" : "PXA.Session.Development";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = requireSecureCookies
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(PxaCookieAuthenticationEvents);
    })
    .AddCookie(PxaAuthenticationSchemes.DesignerCookie, options =>
    {
        options.Cookie.Name = requireSecureCookies
            ? "__Host-PXA.Designer.Session"
            : "PXA.Designer.Session.Development";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = requireSecureCookies
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(PxaCookieAuthenticationEvents);
    });
builder.Services.AddScoped<PxaCookieAuthenticationEvents>();
builder.Services.AddScoped<PxaSessionService>();
builder.Services.AddOptions<PxaPasswordSecurityOptions>()
    .Bind(builder.Configuration.GetSection(PxaPasswordSecurityOptions.SectionName))
    .Validate(
        options => options.BreachedPasswordSha256.All(value =>
            value.Length == 64 && value.All(Uri.IsHexDigit)),
        "Every breached-password fingerprint must be a SHA-256 hexadecimal value.")
    .ValidateOnStart();
builder.Services.AddOptions<PxaAdminSecurityOptions>()
    .Bind(builder.Configuration.GetSection(PxaAdminSecurityOptions.SectionName));
builder.Services.AddScoped<PxaSystemOperatorAccess>();
builder.Services.AddOptions<PxaAccountClosureOptions>()
    .Bind(builder.Configuration.GetSection(PxaAccountClosureOptions.SectionName));
builder.Services.AddOptions<PxaRegistrationOptions>()
    .Bind(builder.Configuration.GetSection(PxaRegistrationOptions.SectionName))
    .Validate(options =>
            !string.IsNullOrWhiteSpace(options.TermsVersion) &&
            options.TermsVersion.Length <= 64 &&
            !string.IsNullOrWhiteSpace(options.PrivacyVersion) &&
            options.PrivacyVersion.Length <= 64 &&
            options.AllowedReturnOrigins.Length > 0,
        "Registration Terms, Privacy versions, and at least one return origin are required.")
    .ValidateOnStart();
builder.Services.AddOptions<PxaDesignerAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(PxaDesignerAuthenticationOptions.SectionName))
    .Validate(options => options.AllowedOrigins.Length > 0,
        "At least one Designer origin must be configured.")
    .ValidateOnStart();
builder.Services.AddOptions<PxaDesignerTemplateOptions>()
    .Bind(builder.Configuration.GetSection(PxaDesignerTemplateOptions.SectionName))
    .Validate(options =>
            options.MaximumDesignJsonBytes is >= 1024 and <= 100 * 1024 * 1024 &&
            options.MaximumPageSize is >= 1 and <= 500,
        "Designer template limits are outside the supported range.")
    .ValidateOnStart();
builder.Services.AddOptions<PxaStorageOptions>()
    .Bind(builder.Configuration.GetSection(PxaStorageOptions.SectionName))
    .Validate(options =>
            string.Equals(options.Provider, "FileSystem", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(options.RootPath) &&
            options.MaximumObjectBytes is >= 1024 and <= 5L * 1024 * 1024 * 1024,
        "Storage provider, root path, or object-size limit is invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<PxaJobOptions>()
    .Bind(builder.Configuration.GetSection(PxaJobOptions.SectionName))
    .Validate(options =>
            options.PollIntervalSeconds is >= 1 and <= 300 &&
            options.LeaseMinutes is >= 1 and <= 1440 &&
            options.MaximumAttempts is >= 1 and <= 20 &&
            options.ResultRetentionDays is >= 1 and <= 3650 &&
            options.CleanupIntervalMinutes is >= 1 and <= 1440 &&
            options.CleanupBatchSize is >= 1 and <= 10000 &&
            options.MetricsIntervalSeconds is >= 5 and <= 300,
        "Background job timing or retry settings are invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, PxaAuthorizationMiddlewareResultHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var problem = PxaApiProblems.Create(
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
        context.User.FindFirst(PxaClaimTypes.ActiveOrganization)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("account-service-accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(PxaClaimTypes.ActiveOrganization)?.Value ??
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
    options.AddPolicy("browser-telemetry", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPxaTenantContext, PxaTenantContext>();
builder.Services.AddScoped<IPxaEntitlementService, PxaEntitlementService>();
builder.Services.AddScoped<IPxaUsageService, PxaUsageService>();
builder.Services.AddSingleton<PxaDesignerProductMetadata>();
builder.Services.AddScoped<IPxaDesignerFeatureGate, PxaDesignerFeatureGate>();
builder.Services.AddOptions<PxaProductAccessOptions>()
    .Bind(builder.Configuration.GetSection("ProductAccess"));
builder.Services.AddOptions<PxaLicensingOptions>()
    .Bind(builder.Configuration.GetSection("Licensing"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.KeyId) &&
                         !string.IsNullOrWhiteSpace(options.PrivateKeyPath) &&
                         !string.IsNullOrWhiteSpace(options.PublicKeyPath) &&
                         options.MetricsIntervalSeconds is >= 15 and <= 3600,
        "Licensing key paths and a metrics interval between 15 and 3600 seconds are required.")
    .ValidateOnStart();
builder.Services.AddSingleton<PxaLicenseSigningService>();
builder.Services.AddSingleton<IPxaLicenseSigningService>(
    services => services.GetRequiredService<PxaLicenseSigningService>());
builder.Services.AddSingleton<IPxaLicenseSignatureVerifier>(
    services => services.GetRequiredService<PxaLicenseSigningService>());
builder.Services.AddSingleton<PxaOfflineLicenseValidator>();
var dataProtectionKeysDirectory = builder.Configuration["DataProtection:KeysDirectory"]
    ?? Path.Combine("App_Data", "data-protection-keys");
var dataProtectionKeysPath = Path.IsPathRooted(dataProtectionKeysDirectory)
    ? dataProtectionKeysDirectory
    : Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeysDirectory);
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("PowerDoxAutomation")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddOptions<PxaMailOptions>()
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
builder.Services.AddScoped<CustomerRegistrationService>();
builder.Services.AddScoped<DesignerAuthorizationCodeService>();
builder.Services.AddScoped<OrganizationMembershipService>();
builder.Services.AddScoped<SubscriptionQueryService>();
builder.Services.AddScoped<IPxaMailQueue, PxaMailQueue>();
builder.Services.AddScoped<PxaMailProcessor>();
builder.Services.AddScoped<PxaMailRetentionService>();
builder.Services.AddScoped<OrganizationNotificationService>();
builder.Services.AddSingleton<PxaMailTemplateRenderer>();
builder.Services.AddSingleton<DevelopmentMailTransport>();
builder.Services.AddSingleton<SmtpMailTransport>();
builder.Services.AddSingleton<DisabledMailTransport>();
builder.Services.AddSingleton<IPxaMailTransport>(services =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PxaMailOptions>>().Value;
    return options.Transport.ToLowerInvariant() switch
    {
        "smtp" => services.GetRequiredService<SmtpMailTransport>(),
        "disabled" => services.GetRequiredService<DisabledMailTransport>(),
        _ => services.GetRequiredService<DevelopmentMailTransport>(),
    };
});
builder.Services.AddScoped<TrialExpiryNotifier>();
builder.Services.AddSingleton<IPxaObjectStorage, FileSystemPxaObjectStorage>();
builder.Services.AddScoped<PxaStoredObjectService>();
builder.Services.AddScoped<IPxaJobQueue, PxaJobQueue>();
builder.Services.AddScoped<PxaJobProcessor>();
builder.Services.AddScoped<PxaJobRetentionService>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<PxaMailWorker>();
    builder.Services.AddHostedService<TrialExpiryWorker>();
    builder.Services.AddHostedService<PxaJobWorker>();
    builder.Services.AddHostedService<PxaJobRetentionWorker>();
    builder.Services.AddHostedService<PxaJobMetricsPublisher>();
    builder.Services.AddHostedService<PxaLicensingMetricsPublisher>();
}
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PxaPermissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim(PxaClaimTypes.Permission, permission));
    foreach (var permission in PxaAccountPermissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim(PxaClaimTypes.Permission, permission));
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = requireSecureCookies ? "__Host-PXA.Antiforgery" : "PXA.Antiforgery.Development";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = requireSecureCookies
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.HeaderName = "X-PXA-CSRF";
});
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PxaDbContext>("pxa-database", tags: ["ready"])
    .AddCheck<PxaMailHealthCheck>("pxa-mail", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddScoped<PxaSystemHealthService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                  "http://localhost:5173",
                  "http://localhost:5174",
                  "http://localhost:5175",
                  "http://localhost:5176",
                  "http://localhost:5177",
                  "http://localhost:5178",
                  "http://localhost:3000",
                  "http://localhost:4173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Register template rendering services
builder.Services.AddScoped<PXA.Domain.Repositories.ITemplateRepository, PostgreSqlTemplateRepository>();

// Register font loader for multi-language PDF support (optional: gracefully absent if fonts dir missing)
builder.Services.AddSingleton<PdfFontLoader>(sp =>
{
    var fontsDir = builder.Configuration["Pdf:FontsDirectory"]
        ?? Path.Combine(AppContext.BaseDirectory, "fonts");
    return new PdfFontLoader(fontsDir);
});

// Register export format exporters
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.HtmlDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.XmlDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.SvgDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.CsvDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.MarkdownDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.ImageDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.JpegDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.TiffDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.OdtDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaWord.WordDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaSpreadsheet.ExcelDocumentExporter>();

// Spreadsheet Editor SDK: workbook (SpreadsheetDto) ⇄ .xlsx round-trip (distinct from the design exporters).
builder.Services.AddScoped<PxaSpreadsheet.ExcelWorkbookExporter>();
builder.Services.AddScoped<PxaSpreadsheet.ExcelWorkbookImporter>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetToDesignConverter>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetCalculator>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetOperations>();
builder.Services.AddScoped<PxaSpreadsheet.XlsWorkbookIo>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetData>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetValidator>();

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
        Microsoft.Extensions.Options.IOptions<PxaObservabilityOptions>>().Value;
    return observability.EnableOcrFailureInjection
        ? new PxaFaultInjectingOcrEngine(engine, observability.OcrFailureInjectionCount)
        : engine;
});
builder.Services.AddTransient<ImageToPdfConverter>();

// Register migration service
builder.Services.AddSingleton<PXA.WebApi.Services.MigrationService>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerAnnotationStore>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerAnnotationFlatteningService>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerNativeAnnotationExtractionService>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerFormExtractionService>();

// Register use cases
builder.Services.AddScoped<PXA.Application.UseCases.ExportDocumentUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.CreateTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.UpdateTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.GetTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.ValidateTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.FindAndReplaceUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.CloneTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.ExtractPagesUseCase>();

var app = builder.Build();

await PxaDevelopmentIdentityBootstrapper.InitializeAsync(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseMiddleware<PxaOperationMetricsMiddleware>();
app.UseAuthentication();
app.UseMiddleware<PxaDesignerAccessMiddleware>();
app.UseMiddleware<PxaDesignerFeatureGateMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<PxaProductAccessMiddleware>();
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

app.Run();

public partial class Program {}
