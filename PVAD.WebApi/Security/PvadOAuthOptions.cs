namespace PVAD.WebApi.Security;

public sealed class PvadOAuthOptions
{
    public const string SectionName = "OAuth";

    public bool Enabled { get; set; } = true;
    public bool MigrateLegacyScopeAliases { get; set; }
    public bool MigrateLegacyClientIdentities { get; set; }
    public string Issuer { get; set; } = "http://localhost:5178";
    public string McpResource { get; set; } = "http://localhost:3000/mcp";
    public string ApiResource { get; set; } = "pvad-api";
    public string PublicClientId { get; set; } = PvadOAuthClients.Public;
    public string McpServerClientId { get; set; } = PvadOAuthClients.McpServer;
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
    public const string DocsRead = "pvad.docs.read";
    public const string TemplatesRead = "pvad.templates.read";
    public const string DocumentsValidate = "pvad.documents.validate";
    public const string DocumentsRender = "pvad.documents.render";

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
        "pvad.templates.write",
        "pvad.assets.read",
        "pvad.assets.write",
        DocumentsValidate,
        DocumentsRender,
        "pvad.documents.export",
        "pvad.documents.import",
        "pvad.migrations.execute",
        "pvad.jobs.read",
        "pvad.jobs.cancel",
    ];
}

public static class PvadOAuthClients
{
    public const string Public = "pvad-mcp-public";
    public const string McpServer = "pvad-mcp-server";
    public const string LegacyPublic = "pxa-mcp-public";
    public const string LegacyMcpServer = "pxa-mcp-server";

    public static string ForApiKey(Guid apiKeyId) => $"pvad-sa-{apiKeyId:N}";
    public static string LegacyForApiKey(Guid apiKeyId) => $"pxa-sa-{apiKeyId:N}";

    public static bool TryParseServiceAccountId(string? clientId, out Guid apiKeyId)
    {
        apiKeyId = Guid.Empty;
        var prefix = clientId?.StartsWith("pvad-sa-", StringComparison.Ordinal) == true ? "pvad-sa-"
            : clientId?.StartsWith("pxa-sa-", StringComparison.Ordinal) == true ? "pxa-sa-" : null;
        return prefix is not null && clientId!.Length == prefix.Length + 32
            && Guid.TryParseExact(clientId[prefix.Length..], "N", out apiKeyId)
            && apiKeyId != Guid.Empty;
    }
}
