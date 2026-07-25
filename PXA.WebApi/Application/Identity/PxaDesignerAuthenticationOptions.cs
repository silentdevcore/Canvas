namespace PXA.WebApi.Application.Identity;

public sealed class PxaDesignerAuthenticationOptions
{
    public const string SectionName = "DesignerAuthentication";

    public string[] AllowedOrigins { get; set; } = [];
}
