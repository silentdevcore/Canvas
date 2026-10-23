using PVAD.WebApi.Application.Identity;

namespace PVAD.WebApi.Security;

public static class PvadCorsConfiguration
{
    public static string[] ResolveOrigins(IConfiguration configuration)
    {
        var section = configuration.GetSection("Cors:AllowedOrigins");
        var values = section.Exists() ? section.Get<string[]>() ?? [] :
        [
            "http://localhost:5173", "http://localhost:5174", "http://localhost:5175",
            "http://localhost:5176", "http://localhost:5177", "http://localhost:5178",
            "http://localhost:3000", "http://localhost:4173",
        ];
        if (values.Length == 0 || values.Length > 64)
            throw new InvalidOperationException("Cors:AllowedOrigins must contain between 1 and 64 explicit origins.");

        return values.Select(value =>
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
                || value.Contains('\\') || value.Contains('*'))
                throw new InvalidOperationException("Invalid Cors:AllowedOrigins entry.");
            return DesignerAuthorizationSecurity.NormalizeOrigin(value)
                ?? throw new InvalidOperationException("Invalid Cors:AllowedOrigins entry.");
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
