using System.Security.Cryptography;
using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Security;

namespace PXA.Importer.Tests;

public sealed class PdfStandardSecurityHandlerTests
{
    [Fact]
    public void Create_ShouldProduceARevision6EncryptDictionary()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions { UserPassword = "open-me" });

        var encrypt = handler.BuildEncryptDictionary();

        Assert.Equal("Standard", Assert.IsType<PdfName>(encrypt["Filter"]).Value);
        Assert.Equal(5, Assert.IsType<PdfInteger>(encrypt["V"]).Value);
        Assert.Equal(6, Assert.IsType<PdfInteger>(encrypt["R"]).Value);
        Assert.Equal(256, Assert.IsType<PdfInteger>(encrypt["Length"]).Value);
        Assert.Equal("StdCF", Assert.IsType<PdfName>(encrypt["StmF"]).Value);
        Assert.Equal("StdCF", Assert.IsType<PdfName>(encrypt["StrF"]).Value);
        var filter = Assert.IsType<PdfDictionary>(Assert.IsType<PdfDictionary>(encrypt["CF"])["StdCF"]);
        Assert.Equal("AESV3", Assert.IsType<PdfName>(filter["CFM"]).Value);
        Assert.Equal(48, Assert.IsType<PdfString>(encrypt["U"]).GetDecodedBytes().Length);
        Assert.Equal(48, Assert.IsType<PdfString>(encrypt["O"]).GetDecodedBytes().Length);
        Assert.Equal(32, Assert.IsType<PdfString>(encrypt["UE"]).GetDecodedBytes().Length);
        Assert.Equal(32, Assert.IsType<PdfString>(encrypt["OE"]).GetDecodedBytes().Length);
        Assert.Equal(16, Assert.IsType<PdfString>(encrypt["Perms"]).GetDecodedBytes().Length);
        Assert.True(PdfStandardSecurityHandler.IsSupported(encrypt));
    }

    [Fact]
    public void Open_ShouldAuthenticateTheUserPasswordAndRecoverTheFileKey()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
        {
            UserPassword = "user-secret",
            OwnerPassword = "owner-secret"
        });
        var ciphertext = handler.EncryptValue("payload"u8);

        var reopened = PdfStandardSecurityHandler.Open(handler.BuildEncryptDictionary(), "user-secret", out var result);

        Assert.Equal(PdfPasswordKind.User, result.PasswordKind);
        Assert.True(result.PermissionsVerified);
        Assert.Equal("payload"u8.ToArray(), reopened.DecryptValue(ciphertext));
    }

    [Fact]
    public void Open_ShouldAuthenticateTheOwnerPasswordAndRecoverTheSameFileKey()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
        {
            UserPassword = "user-secret",
            OwnerPassword = "owner-secret"
        });
        var ciphertext = handler.EncryptValue("payload"u8);

        var reopened = PdfStandardSecurityHandler.Open(handler.BuildEncryptDictionary(), "owner-secret", out var result);

        Assert.Equal(PdfPasswordKind.Owner, result.PasswordKind);
        Assert.Equal("payload"u8.ToArray(), reopened.DecryptValue(ciphertext));
    }

    [Fact]
    public void Open_ShouldAcceptAnEmptyUserPassword()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
        {
            UserPassword = string.Empty,
            OwnerPassword = "owner-secret"
        });

        PdfStandardSecurityHandler.Open(handler.BuildEncryptDictionary(), string.Empty, out var result);

        Assert.Equal(PdfPasswordKind.User, result.PasswordKind);
    }

    [Fact]
    public void Open_ShouldRejectAWrongPasswordWithoutSayingWhichOneFailed()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
        {
            UserPassword = "user-secret",
            OwnerPassword = "owner-secret"
        });

        var exception = Assert.Throws<PdfPasswordException>(
            () => PdfStandardSecurityHandler.Open(handler.BuildEncryptDictionary(), "wrong", out _));

        Assert.DoesNotContain("user", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Open_ShouldRejectTheDocumentWhenThePermsBlockIsTampered()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions { UserPassword = "pw" });
        var encrypt = handler.BuildEncryptDictionary();
        var perms = handler.PermissionsEntry.ToArray();
        perms[0] ^= 0xFF;
        encrypt["Perms"] = new PdfString(perms, IsHex: false);

        Assert.Throws<PdfEncryptionIntegrityException>(
            () => PdfStandardSecurityHandler.Open(encrypt, "pw", out _));
    }

    [Fact]
    public void Open_ShouldRejectMalformedReservedPermissionBits()
    {
        using var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = "pw" });
        var encrypt = handler.BuildEncryptDictionary();
        var permissions = Assert.IsType<PdfInteger>(encrypt["P"]).Value;
        encrypt["P"] = new PdfInteger(permissions | 1);

        Assert.Throws<PdfEncryptionIntegrityException>(
            () => PdfStandardSecurityHandler.Open(encrypt, "pw", out _));
    }

    [Fact]
    public void Open_ShouldDecodeThePermissionsThatWereGranted()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
        {
            UserPassword = "pw",
            OwnerPassword = "owner-secret",
            Permissions = PdfDocumentPermissions.Print | PdfDocumentPermissions.ExtractForAccessibility
        });

        PdfStandardSecurityHandler.Open(handler.BuildEncryptDictionary(), "pw", out var result);

        Assert.True(result.PermissionsVerified);
        Assert.Equal(
            PdfDocumentPermissions.Print | PdfDocumentPermissions.ExtractForAccessibility,
            result.Permissions);
        Assert.False(result.Permissions.HasFlag(PdfDocumentPermissions.ModifyContents));
    }

    [Fact]
    public void Open_ShouldRoundTripEverySupportedPermissionCombination()
    {
        var supportedPermissions = new[]
        {
            PdfDocumentPermissions.Print,
            PdfDocumentPermissions.ModifyContents,
            PdfDocumentPermissions.Copy,
            PdfDocumentPermissions.ModifyAnnotations,
            PdfDocumentPermissions.FillForms,
            PdfDocumentPermissions.ExtractForAccessibility,
            PdfDocumentPermissions.AssembleDocument,
            PdfDocumentPermissions.PrintHighResolution,
        };

        for (var mask = 0; mask < (1 << supportedPermissions.Length); mask++)
        {
            var permissions = PdfDocumentPermissions.None;
            for (var bit = 0; bit < supportedPermissions.Length; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    permissions |= supportedPermissions[bit];
                }
            }

            using var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
            {
                UserPassword = "user-secret",
                OwnerPassword = "owner-secret",
                Permissions = permissions,
            });

            using var reopened = PdfStandardSecurityHandler.Open(
                handler.BuildEncryptDictionary(),
                "user-secret",
                out var result);

            Assert.True(result.PermissionsVerified);
            Assert.Equal(permissions, result.Permissions);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_ShouldPreserveTheEncryptMetadataOption(bool encryptMetadata)
    {
        using var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
        {
            UserPassword = "user-secret",
            OwnerPassword = "owner-secret",
            EncryptMetadata = encryptMetadata,
        });

        var encrypt = handler.BuildEncryptDictionary();
        using var reopened = PdfStandardSecurityHandler.Open(encrypt, "user-secret", out var result);

        Assert.Equal(encryptMetadata, Assert.IsType<PdfBoolean>(encrypt["EncryptMetadata"]).Value);
        Assert.Equal(encryptMetadata, result.EncryptMetadata);
    }

    [Fact]
    public void EncryptValue_ShouldProduceDifferentCiphertextForTheSameInput()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions { UserPassword = "pw" });

        var first = handler.EncryptValue("same"u8);
        var second = handler.EncryptValue("same"u8);

        Assert.NotEqual(first, second);
        Assert.Equal(handler.DecryptValue(first), handler.DecryptValue(second));
    }

    [Fact]
    public void EncryptValue_ShouldRoundTripEmptyAndBlockAlignedPayloads()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions { UserPassword = "pw" });

        foreach (var length in new[] { 0, 1, 15, 16, 17, 4096 })
        {
            var payload = RandomNumberGenerator.GetBytes(length);
            Assert.Equal(payload, handler.DecryptValue(handler.EncryptValue(payload)));
        }
    }

    [Fact]
    public void DecryptValue_ShouldRejectAMalformedBlock()
    {
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions { UserPassword = "pw" });

        Assert.Throws<PdfPasswordException>(() => handler.DecryptValue(new byte[20]));
    }

    [Theory]
    [InlineData("café", "café")]
    public void NormalizePassword_ShouldNormalizeEquivalentUnicodeForms(string composed, string decomposed) =>
        Assert.Equal(
            PdfStandardSecurityHandler.NormalizePassword(composed),
            PdfStandardSecurityHandler.NormalizePassword(decomposed));

    [Fact]
    public void NormalizePassword_ShouldCapAt127BytesAfterEncoding()
    {
        // Each character encodes to two bytes, so the cap must bite at 63 characters, not 127.
        var password = new string('é', 200);

        var normalized = PdfStandardSecurityHandler.NormalizePassword(password);

        Assert.Equal(127, normalized.Length);
    }

    [Fact]
    public void NormalizePassword_ShouldApplySaslPrepMappings()
    {
        Assert.Equal(
            PdfStandardSecurityHandler.NormalizePassword("space here"),
            PdfStandardSecurityHandler.NormalizePassword("space\u00A0here"));
        Assert.Equal(
            PdfStandardSecurityHandler.NormalizePassword("soft-hyphen"),
            PdfStandardSecurityHandler.NormalizePassword("soft\u00AD-hyphen"));
    }

    [Theory]
    [InlineData("control\u0007")]
    [InlineData("private\uE000")]
    [InlineData("unassigned\u0221")]
    [InlineData("invalid\uD800")]
    [InlineData("a\u05D0")]
    public void NormalizePassword_ShouldRejectProhibitedAndBidirectionalInput(string password) =>
        Assert.Throws<ArgumentException>(() => PdfStandardSecurityHandler.NormalizePassword(password));

    [Fact]
    public void NormalizePassword_ShouldAcceptAWellFormedRightToLeftPassword()
    {
        var normalized = PdfStandardSecurityHandler.NormalizePassword("\u05D0-123-\u05D1");

        Assert.NotEmpty(normalized);
    }

    [Fact]
    public void Open_ShouldAuthenticateAPasswordLongerThanTheLimitByItsTruncatedForm()
    {
        var password = new string('a', 200);
        // A distinct owner password, so authenticating as User is unambiguous: when the owner
        // password defaults to the user password, the owner check matches first by design.
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions
        {
            UserPassword = password,
            OwnerPassword = "a-different-owner-password"
        });

        PdfStandardSecurityHandler.Open(handler.BuildEncryptDictionary(), new string('a', 127), out var result);

        Assert.Equal(PdfPasswordKind.User, result.PasswordKind);
    }

    [Fact]
    public void Open_ShouldPreferTheOwnerResultWhenBothPasswordsMatch()
    {
        // Create defaults the owner password to the user password, so one value opens both.
        var handler = PdfStandardSecurityHandler.Create(new PdfDocumentEncryptionOptions { UserPassword = "shared" });

        PdfStandardSecurityHandler.Open(handler.BuildEncryptDictionary(), "shared", out var result);

        Assert.Equal(PdfPasswordKind.Owner, result.PasswordKind);
    }

    [Fact]
    public void Open_ShouldRejectUnsupportedRevisions()
    {
        var encrypt = PdfStandardSecurityHandler
            .Create(new PdfDocumentEncryptionOptions { UserPassword = "pw" })
            .BuildEncryptDictionary();
        encrypt["R"] = new PdfInteger(4);

        Assert.False(PdfStandardSecurityHandler.IsSupported(encrypt));
        var exception = Assert.Throws<NotSupportedException>(
            () => PdfStandardSecurityHandler.Open(encrypt, "pw", out _));
        Assert.Contains("/R 4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_ShouldRejectASelectedCryptFilterThatIsNotAesV3OrIdentity()
    {
        using var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = "pw" });
        var encrypt = handler.BuildEncryptDictionary();
        var filter = Assert.IsType<PdfDictionary>(Assert.IsType<PdfDictionary>(encrypt["CF"])["StdCF"]);
        filter["CFM"] = new PdfName("AESV2");

        Assert.False(PdfStandardSecurityHandler.IsSupported(encrypt));
        Assert.Throws<NotSupportedException>(
            () => PdfStandardSecurityHandler.Open(encrypt, "pw", out _));
    }

    [Fact]
    public void Open_ShouldRejectAnUnsupportedCryptFilterAuthenticationEvent()
    {
        using var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = "pw" });
        var encrypt = handler.BuildEncryptDictionary();
        var filter = Assert.IsType<PdfDictionary>(Assert.IsType<PdfDictionary>(encrypt["CF"])["StdCF"]);
        filter["AuthEvent"] = new PdfName("EFOpen");

        Assert.False(PdfStandardSecurityHandler.IsSupported(encrypt));
        Assert.Throws<NotSupportedException>(
            () => PdfStandardSecurityHandler.Open(encrypt, "pw", out _));
    }

    [Fact]
    public void Open_ShouldSupportIdentityForUnencryptedStringsAndStreams()
    {
        using var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = "pw" });
        var encrypt = handler.BuildEncryptDictionary();
        encrypt["StrF"] = new PdfName("Identity");
        encrypt["StmF"] = new PdfName("Identity");

        using var reopened = PdfStandardSecurityHandler.Open(encrypt, "pw", out var result);

        Assert.True(result.PermissionsVerified);
        Assert.False(reopened.ShouldDecryptString);
        Assert.False(reopened.ShouldDecryptStream(new PdfDictionary()));
    }

    [Fact]
    public void Open_ShouldRejectAnExplicitStreamCryptFilterUntilItCanBeInterpreted()
    {
        using var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = "pw" });
        using var reopened = PdfStandardSecurityHandler.Open(
            handler.BuildEncryptDictionary(), "pw", out _);
        var streamDictionary = new PdfDictionary { ["Filter"] = new PdfName("Crypt") };

        Assert.Throws<NotSupportedException>(() => reopened.ShouldDecryptStream(streamDictionary));
    }

    [Fact]
    public void Create_ShouldRequireADistinctOwnerPasswordForRestrictedPermissions()
    {
        var missing = Assert.Throws<ArgumentException>(() => PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions
            {
                UserPassword = "pw",
                Permissions = PdfDocumentPermissions.Print
            }));
        Assert.Contains("distinct", missing.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Throws<ArgumentException>(() => PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions
            {
                UserPassword = "pw",
                OwnerPassword = "pw",
                Permissions = PdfDocumentPermissions.Print
            }));
    }

    [Fact]
    public void Open_ShouldRejectOversizedSecurityEntriesInsteadOfTruncatingThem()
    {
        using var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = "pw" });
        var encrypt = handler.BuildEncryptDictionary();
        encrypt["UE"] = new PdfString(new byte[33], IsHex: false);

        Assert.False(PdfStandardSecurityHandler.IsSupported(encrypt));
        Assert.Throws<NotSupportedException>(
            () => PdfStandardSecurityHandler.Open(encrypt, "pw", out _));
    }

    [Fact]
    public void DisposedHandler_ShouldEraseItsFileKeyAndRejectFurtherUse()
    {
        var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = "pw" });

        handler.Dispose();

        Assert.Throws<ObjectDisposedException>(() => handler.EncryptValue("payload"u8));
        Assert.Throws<ObjectDisposedException>(() => handler.BuildEncryptDictionary());
    }

    [Fact]
    public void HardenedHash_ShouldBeDeterministicAndSaltDependent()
    {
        var password = Encoding.UTF8.GetBytes("pw");
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var other = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };

        Assert.Equal(
            PdfStandardSecurityHandler.HardenedHash(password, salt, []),
            PdfStandardSecurityHandler.HardenedHash(password, salt, []));
        Assert.NotEqual(
            PdfStandardSecurityHandler.HardenedHash(password, salt, []),
            PdfStandardSecurityHandler.HardenedHash(password, other, []));
        Assert.Equal(32, PdfStandardSecurityHandler.HardenedHash(password, salt, []).Length);
    }
}
