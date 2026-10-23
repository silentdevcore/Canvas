using System.Security.Cryptography.X509Certificates;
using OpenIddict.Abstractions;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Security;

public static class PvadOAuthServiceCollectionExtensions
{
    public static IServiceCollection AddPvadOAuthServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(PvadOAuthOptions.SectionName);
        services.AddOptions<PvadOAuthOptions>()
            .Bind(section)
            .Validate(options =>
                !options.Enabled ||
                Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer) &&
                issuer.Scheme is "http" or "https",
                "OAuth issuer must be an absolute HTTP(S) URL.")
            .Validate(options =>
                !options.Enabled || Uri.TryCreate(options.McpResource, UriKind.Absolute, out _),
                "OAuth MCP resource must be an absolute URI.")
            .Validate(options =>
                !options.Enabled || options.PublicClientRedirectUris.All(value =>
                    Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                    uri.Scheme is "http" or "https"),
                "OAuth public-client redirect URIs must be absolute HTTP(S) URLs.")
            .Validate(options => options.DynamicClientMaximumRedirectUris is >= 1 and <= 20,
                "OAuth dynamic-client redirect URI limit must be between 1 and 20.")
            .Validate(options =>
                string.IsNullOrWhiteSpace(options.McpDelegationSecret) ||
                System.Text.Encoding.UTF8.GetByteCount(options.McpDelegationSecret) >= 32,
                "OAuth MCP delegation secret must contain at least 32 bytes when configured.")
            .ValidateOnStart();

        var settings = section.Get<PvadOAuthOptions>() ?? new PvadOAuthOptions();
        if (!settings.Enabled)
        {
            services.AddScoped<IPvadOAuthClientRegistry, DisabledPvadOAuthClientRegistry>();
            return services;
        }

        services.AddScoped<IPvadOAuthClientRegistry, PvadOAuthClientRegistry>();

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<PvadDbContext>())
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(settings.Issuer));
                options.SetAuthorizationEndpointUris("connect/authorize");
                options.SetTokenEndpointUris("connect/token");
                options.SetJsonWebKeySetEndpointUris(".well-known/jwks.json");
                options.AllowAuthorizationCodeFlow();
                options.AllowClientCredentialsFlow();
                options.RequireProofKeyForCodeExchange();
                options.RegisterScopes(PvadScopeCompatibility.SupportedScopes);
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
                options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(2));
                options.DisableAccessTokenEncryption();
                if (settings.DynamicClientRegistrationEnabled)
                {
                    options.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ApplyConfigurationResponseContext>(builder =>
                        builder.UseInlineHandler(context =>
                        {
                            context.Response["registration_endpoint"] =
                                settings.Issuer.TrimEnd('/') + "/connect/register";
                            return default;
                        }));
                }

                if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                {
                    options.AddEphemeralEncryptionKey();
                    options.AddEphemeralSigningKey();
                    options.UseAspNetCore().DisableTransportSecurityRequirement();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(settings.SigningCertificatePath))
                        throw new InvalidOperationException(
                            "OAuth signing certificate configuration is required outside development.");
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        settings.SigningCertificatePath,
                        settings.SigningCertificatePassword);
                    options.AddEncryptionCertificate(certificate);
                    options.AddSigningCertificate(certificate);
                }

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}
