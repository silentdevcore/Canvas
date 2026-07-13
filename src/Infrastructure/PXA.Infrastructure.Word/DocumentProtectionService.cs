using System.Security.Cryptography;
using System.Text;
using PXA.Core.Contracts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PXA.Infrastructure.Word;

/// <summary>
/// Applies document-level write protection and editing restrictions to a DOCX.
/// </summary>
internal static class DocumentProtectionService
{
    // Word's legacy password-protection scheme: SHA-1, RSA-full provider, 50 000 spins.
    private const int    CryptAlgorithmSid = 4; // SHA-1
    private const uint   SpinCount         = 50_000;
    private const string ProviderType      = "rsaFull";

    internal static void Apply(WordprocessingDocument doc, DocumentProtectionDto? protection)
    {
        if (protection is null || !protection.Enabled) return;

        var settings = doc.MainDocumentPart!.DocumentSettingsPart
            ?? doc.MainDocumentPart.AddNewPart<DocumentSettingsPart>();

        settings.Settings ??= new Settings();

        var editRestriction = protection.Mode switch
        {
            "comments"       => DocumentProtectionValues.Comments,
            "trackedChanges" => DocumentProtectionValues.TrackedChanges,
            "formFields"     => DocumentProtectionValues.Forms,
            _                => DocumentProtectionValues.ReadOnly,
        };

        var dp = new DocumentProtection
        {
            Edit = editRestriction,
            Enforcement = true,
        };

        // The DTO carries the user-supplied password. Word stores a salted, iterated
        // SHA-1 hash (never the password itself), so we derive that here.
        if (!string.IsNullOrEmpty(protection.PasswordHash))
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = ComputeLegacyHash(protection.PasswordHash, salt);

            dp.CryptographicProviderType  = CryptProviderValues.RsaFull;
            dp.CryptographicAlgorithmClass = CryptAlgorithmClassValues.Hash;
            dp.CryptographicAlgorithmType  = CryptAlgorithmValues.TypeAny;
            dp.CryptographicAlgorithmSid   = CryptAlgorithmSid;
            dp.CryptographicSpinCount      = SpinCount;
            dp.Hash = Convert.ToBase64String(hash);
            dp.Salt = Convert.ToBase64String(salt);
        }

        // Remove any previous protection element before inserting.
        settings.Settings.RemoveAllChildren<DocumentProtection>();
        settings.Settings.InsertAt(dp, 0);
        settings.Settings.Save();
    }

    /// <summary>
    /// Word's legacy password verifier (ECMA-376 / MS-OFFCRYPTO):
    /// H₀ = SHA1(salt ‖ passwordUTF16LE); Hᵢ = SHA1(Hᵢ₋₁ ‖ uint32LE(i-1)) for <see cref="SpinCount"/> rounds.
    /// </summary>
    private static byte[] ComputeLegacyHash(string password, byte[] salt)
    {
        var pwdBytes = Encoding.Unicode.GetBytes(password); // UTF-16LE

        var initial = new byte[salt.Length + pwdBytes.Length];
        Buffer.BlockCopy(salt, 0, initial, 0, salt.Length);
        Buffer.BlockCopy(pwdBytes, 0, initial, salt.Length, pwdBytes.Length);

        var hash = SHA1.HashData(initial);

        var buffer = new byte[hash.Length + 4];
        for (uint i = 0; i < SpinCount; i++)
        {
            Buffer.BlockCopy(hash, 0, buffer, 0, hash.Length);
            BitConverter.GetBytes(i).CopyTo(buffer, hash.Length); // uint32 little-endian
            hash = SHA1.HashData(buffer);
        }

        return hash;
    }
}
