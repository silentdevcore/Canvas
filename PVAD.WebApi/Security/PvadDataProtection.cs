using Microsoft.AspNetCore.DataProtection;

namespace PVAD.WebApi.Security;

public static class PvadDataProtection
{
    // Payloads protected under another discriminator need PvadProtectedDataReprotection first.
    public const string ApplicationName = "PVAD";
    public const string MailPayloadPurpose = "PVAD.Mail.Outbox.Payload.v1";
    public const string RevocationEvidencePurpose = "PVAD.PdfTrust.RevocationEvidence.v1";

    public static string KeysPath(IConfiguration configuration, IHostEnvironment environment)
    {
        var directory = configuration["DataProtection:KeysDirectory"] ?? Path.Combine("App_Data", "data-protection-keys");
        return Path.IsPathRooted(directory) ? directory : Path.Combine(environment.ContentRootPath, directory);
    }

    public static IDataProtector MailPayload(IDataProtectionProvider provider) =>
        provider.CreateProtector(MailPayloadPurpose);

    public static IDataProtector RevocationEvidence(IDataProtectionProvider provider) =>
        provider.CreateProtector(RevocationEvidencePurpose);
}
