using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace PVAD.WebApi.Security;

public static class PvadDataProtection
{
    // Changing the application discriminator would make the existing keyring unusable.
    public const string ApplicationName = "PowerDoxAutomation";
    public const string MailPayloadPurpose = "PVAD.Mail.Outbox.Payload.v1";
    public const string LegacyMailPayloadPurpose = "PXA.Mail.Outbox.Payload.v1";
    public const string RevocationEvidencePurpose = "PVAD.PdfTrust.RevocationEvidence.v1";
    public const string LegacyRevocationEvidencePurpose = "PXA.PdfTrust.RevocationEvidence.v1";

    public static IDataProtector MailPayload(IDataProtectionProvider provider) =>
        new CompatibleProtector(provider.CreateProtector(MailPayloadPurpose), provider.CreateProtector(LegacyMailPayloadPurpose));

    public static IDataProtector RevocationEvidence(IDataProtectionProvider provider) =>
        new CompatibleProtector(provider.CreateProtector(RevocationEvidencePurpose), provider.CreateProtector(LegacyRevocationEvidencePurpose));

    private sealed class CompatibleProtector(IDataProtector current, IDataProtector legacy) : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) =>
            new CompatibleProtector(current.CreateProtector(purpose), legacy.CreateProtector(purpose));

        public byte[] Protect(byte[] plaintext) => current.Protect(plaintext);

        public byte[] Unprotect(byte[] protectedData)
        {
            try { return current.Unprotect(protectedData); }
            catch (CryptographicException) { return legacy.Unprotect(protectedData); }
        }
    }
}
