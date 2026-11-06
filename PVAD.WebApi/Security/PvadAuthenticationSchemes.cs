namespace PVAD.WebApi.Security;

public static class PvadAuthenticationSchemes
{
    public const string Combined = "PVAD.Combined";
    public const string ApiKey = "PVAD.ApiKey";
    public const string McpDelegation = "PVAD.McpDelegation";
    public const string DesignerCookie = "PVAD.Designer.Cookie";

    public const string LegacyCombined = "PXA.Combined";
    public const string LegacyApiKey = "PXA.ApiKey";
    public const string LegacyMcpDelegation = "PXA.McpDelegation";
    public const string LegacyDesignerCookie = "PXA.Designer.Cookie";
}
