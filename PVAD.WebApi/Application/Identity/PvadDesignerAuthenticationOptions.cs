namespace PVAD.WebApi.Application.Identity;

public sealed class PvadDesignerAuthenticationOptions
{
    public const string SectionName = "DesignerAuthentication";

    public string[] AllowedOrigins { get; set; } = [];
}
