namespace PVAD.WebApi.Security;

public sealed class PvadOAuthOptions
{
    public const string SectionName = "OAuth";

    public bool Enabled { get; set; } = true;
    public bool MigrateLegacyScopeAliases { get; set; }
    public string Issuer { get; set; } = "http://localhost:5178";
    public string McpResource { get; set; } = "http://localhost:3000/mcp";
    public string ApiResource { get; set; } = "pxa-api";
    public string PublicClientId { get; set; } = "pxa-mcp-public";
    public List<string> PublicClientRedirectUris { get; set; } = [];
    public bool DynamicClientRegistrationEnabled { get; set; } = true;
    public int DynamicClientMaximumRedirectUris { get; set; } = 10;
    public string? McpServerClientSecret { get; set; }
    public string? McpDelegationSecret { get; set; }
    public string? SigningCertificatePath { get; set; }
    public string? SigningCertificatePassword { get; set; }
}

public static class PvadOAuthScopes
{
    public const string DocsRead = "pxa.docs.read";
    public const string TemplatesRead = "pxa.templates.read";
    public const string DocumentsValidate = "pxa.documents.validate";
    public const string DocumentsRender = "pxa.documents.render";

    public static readonly string[] ReadOnly =
    [
        DocsRead,
        TemplatesRead,
        DocumentsValidate,
    ];

    public static readonly string[] All =
    [
        DocsRead,
        TemplatesRead,
        "pxa.templates.write",
        "pxa.assets.read",
        "pxa.assets.write",
        DocumentsValidate,
        DocumentsRender,
        "pxa.documents.export",
        "pxa.documents.import",
        "pxa.migrations.execute",
        "pxa.jobs.read",
        "pxa.jobs.cancel",
    ];
}

public static class PvadOAuthClients
{
    public const string McpServer = "pxa-mcp-server";

    public static string ForApiKey(Guid apiKeyId) => $"pxa-sa-{apiKeyId:N}";
}
