using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using PXA.Importer.Objects;

namespace PXA.Importer.Security;

/// <summary>
/// The PDF 2.0 Standard Security Handler at revision 6 (AES-256).
///
/// All primitives come from the platform: AES, SHA-2, the random generator, and the
/// fixed-time comparison. The only thing implemented here is the sequencing the standard
/// prescribes.
/// </summary>
public sealed class PdfStandardSecurityHandler : IPdfDocumentSecurityHandler
{
    private const int Version = 5;
    private const int Revision = 6;
    private const int FileKeyLength = 32;
    private const int SaltLength = 8;
    private const int HashLength = 32;
    private const int PasswordByteLimit = 127;

    private readonly byte[] _fileKey;
    private bool _disposed;

    private PdfStandardSecurityHandler(
        byte[] fileKey,
        byte[] ownerEntry,
        byte[] userEntry,
        byte[] ownerKeyEntry,
        byte[] userKeyEntry,
        byte[] permissionsEntry,
        int permissionFlags,
        bool encryptMetadata,
        bool encryptStrings,
        bool encryptStreams,
        bool encryptEmbeddedFiles)
    {
        _fileKey = fileKey;
        OwnerEntry = ownerEntry;
        UserEntry = userEntry;
        OwnerKeyEntry = ownerKeyEntry;
        UserKeyEntry = userKeyEntry;
        PermissionsEntry = permissionsEntry;
        PermissionFlags = permissionFlags;
        EncryptMetadata = encryptMetadata;
        EncryptStrings = encryptStrings;
        EncryptStreams = encryptStreams;
        EncryptEmbeddedFiles = encryptEmbeddedFiles;
    }

    public byte[] OwnerEntry { get; }
    public byte[] UserEntry { get; }
    public byte[] OwnerKeyEntry { get; }
    public byte[] UserKeyEntry { get; }
    public byte[] PermissionsEntry { get; }
    public int PermissionFlags { get; }
    public bool EncryptMetadata { get; }
    internal bool EncryptStrings { get; }
    internal bool EncryptStreams { get; }
    internal bool EncryptEmbeddedFiles { get; }

    /// <summary>Builds a handler and a fresh random file encryption key for a new document.</summary>
    public static PdfStandardSecurityHandler Create(PdfDocumentEncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Algorithm != PdfDocumentEncryptionAlgorithm.Aes256)
        {
            throw new NotSupportedException("Only AES-256 (/V 5 /R 6) is supported for new documents.");
        }

        var userPassword = NormalizePassword(options.UserPassword);
        var ownerPassword = NormalizePassword(
            options.OwnerPassword ?? options.UserPassword);

        if (options.Permissions != PdfDocumentPermissions.All &&
            (string.IsNullOrEmpty(options.OwnerPassword) ||
             CryptographicOperations.FixedTimeEquals(userPassword, ownerPassword)))
        {
            CryptographicOperations.ZeroMemory(userPassword);
            CryptographicOperations.ZeroMemory(ownerPassword);
            throw new ArgumentException(
                "Restricted PDF permissions require an owner password distinct from the user password.",
                nameof(options));
        }

        var fileKey = RandomNumberGenerator.GetBytes(FileKeyLength);
        byte[]? userValidationHash = null;
        byte[]? userKeyHash = null;
        byte[]? ownerValidationHash = null;
        byte[]? ownerKeyHash = null;
        try
        {
            // Algorithm 8: /U and /UE.
            var userValidationSalt = RandomNumberGenerator.GetBytes(SaltLength);
            var userKeySalt = RandomNumberGenerator.GetBytes(SaltLength);
            var userEntry = new byte[48];
            userValidationHash = HardenedHash(userPassword, userValidationSalt, []);
            userValidationHash.CopyTo(userEntry, 0);
            userValidationSalt.CopyTo(userEntry, HashLength);
            userKeySalt.CopyTo(userEntry, HashLength + SaltLength);
            userKeyHash = HardenedHash(userPassword, userKeySalt, []);
            var userKeyEntry = AesNoPadding(userKeyHash, fileKey, encrypt: true);

            // Algorithm 9: /O and /OE, bound to the complete /U value.
            var ownerValidationSalt = RandomNumberGenerator.GetBytes(SaltLength);
            var ownerKeySalt = RandomNumberGenerator.GetBytes(SaltLength);
            var ownerEntry = new byte[48];
            ownerValidationHash = HardenedHash(ownerPassword, ownerValidationSalt, userEntry);
            ownerValidationHash.CopyTo(ownerEntry, 0);
            ownerValidationSalt.CopyTo(ownerEntry, HashLength);
            ownerKeySalt.CopyTo(ownerEntry, HashLength + SaltLength);
            ownerKeyHash = HardenedHash(ownerPassword, ownerKeySalt, userEntry);
            var ownerKeyEntry = AesNoPadding(ownerKeyHash, fileKey, encrypt: true);

            var permissionFlags = ToPermissionFlags(options.Permissions);
            var permissionsEntry = EncodePermissions(fileKey, permissionFlags, options.EncryptMetadata);

            return new PdfStandardSecurityHandler(
                fileKey,
                ownerEntry,
                userEntry,
                ownerKeyEntry,
                userKeyEntry,
                permissionsEntry,
                permissionFlags,
                options.EncryptMetadata,
                encryptStrings: true,
                encryptStreams: true,
                encryptEmbeddedFiles: true);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(fileKey);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(userPassword);
            CryptographicOperations.ZeroMemory(ownerPassword);
            Zero(userValidationHash);
            Zero(userKeyHash);
            Zero(ownerValidationHash);
            Zero(ownerKeyHash);
        }
    }

    /// <summary>
    /// Algorithm 2.A. Authenticates against an existing <c>/Encrypt</c> dictionary and
    /// recovers the file encryption key.
    /// </summary>
    public static PdfStandardSecurityHandler Open(
        PdfDictionary encrypt,
        string password,
        out PdfDecryptionResult result)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        EnsureSupported(encrypt);

        var userEntry = ReadFixedString(encrypt, "U", 48);
        var ownerEntry = ReadFixedString(encrypt, "O", 48);
        var userKeyEntry = ReadFixedString(encrypt, "UE", 32);
        var ownerKeyEntry = ReadFixedString(encrypt, "OE", 32);
        var permissionsEntry = ReadFixedString(encrypt, "Perms", 16);
        var permissionFlags = ReadInteger(encrypt["P"]);
        if (!ArePermissionFlagsWellFormed(permissionFlags))
        {
            throw new PdfEncryptionIntegrityException(
                "The encrypted document permission flags contain invalid reserved bits.");
        }

        var encryptMetadata = encrypt["EncryptMetadata"] is not PdfBoolean { Value: false };
        var cryptFilters = ReadCryptFilters(encrypt);
        var normalized = NormalizePassword(password);

        byte[]? fileKey = null;
        var kind = PdfPasswordKind.None;

        // The owner check is attempted first so that a password which is valid for both
        // yields the stronger result.
        byte[]? candidateHash = null;
        try
        {
            candidateHash = HardenedHash(
                normalized, ownerEntry.AsSpan(HashLength, SaltLength).ToArray(), userEntry);
            if (CryptographicOperations.FixedTimeEquals(
                    candidateHash,
                    ownerEntry.AsSpan(0, HashLength)))
            {
                Zero(candidateHash);
                candidateHash = HardenedHash(
                    normalized, ownerEntry.AsSpan(HashLength + SaltLength, SaltLength).ToArray(), userEntry);
                fileKey = AesNoPadding(candidateHash, ownerKeyEntry, encrypt: false);
                kind = PdfPasswordKind.Owner;
            }
            else
            {
                Zero(candidateHash);
                candidateHash = HardenedHash(
                    normalized, userEntry.AsSpan(HashLength, SaltLength).ToArray(), []);
                if (CryptographicOperations.FixedTimeEquals(
                        candidateHash,
                        userEntry.AsSpan(0, HashLength)))
                {
                    Zero(candidateHash);
                    candidateHash = HardenedHash(
                        normalized, userEntry.AsSpan(HashLength + SaltLength, SaltLength).ToArray(), []);
                    fileKey = AesNoPadding(candidateHash, userKeyEntry, encrypt: false);
                    kind = PdfPasswordKind.User;
                }
            }

            if (fileKey is null)
            {
                throw new PdfPasswordException("The supplied password does not open this document.");
            }

            if (!VerifyPermissions(fileKey, permissionsEntry, permissionFlags, encryptMetadata))
            {
                throw new PdfEncryptionIntegrityException(
                    "The encrypted document permission block failed integrity validation.");
            }

            result = new PdfDecryptionResult(
                kind,
                FromPermissionFlags(permissionFlags),
                encryptMetadata,
                PermissionsVerified: true);

            return new PdfStandardSecurityHandler(
                fileKey,
                ownerEntry,
                userEntry,
                ownerKeyEntry,
                userKeyEntry,
                permissionsEntry,
                permissionFlags,
                encryptMetadata,
                cryptFilters.EncryptStrings,
                cryptFilters.EncryptStreams,
                cryptFilters.EncryptEmbeddedFiles);
        }
        catch
        {
            Zero(fileKey);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(normalized);
            Zero(candidateHash);
        }
    }

    /// <summary>True when the dictionary declares a revision this handler implements.</summary>
    public static bool IsSupported(PdfDictionary? encrypt)
    {
        if (encrypt is null)
        {
            return false;
        }

        try
        {
            EnsureSupported(encrypt);
            ReadCryptFilters(encrypt);
            ReadFixedString(encrypt, "U", 48);
            ReadFixedString(encrypt, "O", 48);
            ReadFixedString(encrypt, "UE", 32);
            ReadFixedString(encrypt, "OE", 32);
            ReadFixedString(encrypt, "Perms", 16);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    internal bool ShouldDecryptString => EncryptStrings;

    bool IPdfDocumentSecurityHandler.ShouldDecryptString => ShouldDecryptString;

    internal bool ShouldDecryptStream(PdfDictionary dictionary)
    {
        if (dictionary["Type"] is PdfName { Value: "XRef" })
        {
            return false;
        }

        if (ContainsExplicitCryptFilter(dictionary["Filter"]))
        {
            throw new NotSupportedException(
                "Streams with an explicit /Crypt filter are not supported yet.");
        }

        return dictionary["Type"] is PdfName { Value: "EmbeddedFile" }
            ? EncryptEmbeddedFiles
            : EncryptStreams;
    }

    bool IPdfDocumentSecurityHandler.ShouldDecryptStream(PdfDictionary dictionary) =>
        ShouldDecryptStream(dictionary);

    /// <summary>
    /// Encrypts one string or stream payload. Revision 6 uses the file key directly, so there
    /// is no per-object key derivation.
    /// </summary>
    public byte[] EncryptValue(ReadOnlySpan<byte> plaintext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = _fileKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var ciphertext = aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);
        var result = new byte[iv.Length + ciphertext.Length];
        iv.CopyTo(result, 0);
        ciphertext.CopyTo(result, iv.Length);
        return result;
    }

    public byte[] DecryptValue(ReadOnlySpan<byte> ciphertext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ciphertext.Length < 32 || (ciphertext.Length - 16) % 16 != 0)
        {
            // Not a well-formed AESV3 value; returning it unchanged would silently present
            // ciphertext as content.
            throw new PdfPasswordException("An encrypted value is not a well-formed AES-256 block.");
        }

        using var aes = Aes.Create();
        aes.Key = _fileKey;
        aes.Mode = CipherMode.CBC;
        return aes.DecryptCbc(ciphertext[16..], ciphertext[..16], PaddingMode.PKCS7);
    }

    byte[] IPdfDocumentSecurityHandler.DecryptValue(
        ReadOnlySpan<byte> ciphertext,
        int objectNumber,
        int generation) => DecryptValue(ciphertext);

    /// <summary>Builds the <c>/Encrypt</c> dictionary for a newly encrypted document.</summary>
    public PdfDictionary BuildEncryptDictionary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var cryptFilter = new PdfDictionary
        {
            ["CFM"] = new PdfName("AESV3"),
            ["AuthEvent"] = new PdfName("DocOpen"),
            ["Length"] = new PdfInteger(FileKeyLength)
        };

        return new PdfDictionary
        {
            ["Filter"] = new PdfName("Standard"),
            ["V"] = new PdfInteger(Version),
            ["R"] = new PdfInteger(Revision),
            ["Length"] = new PdfInteger(FileKeyLength * 8),
            ["CF"] = new PdfDictionary { ["StdCF"] = cryptFilter },
            ["StmF"] = new PdfName("StdCF"),
            ["StrF"] = new PdfName("StdCF"),
            ["U"] = new PdfString(UserEntry, IsHex: false),
            ["UE"] = new PdfString(UserKeyEntry, IsHex: false),
            ["O"] = new PdfString(OwnerEntry, IsHex: false),
            ["OE"] = new PdfString(OwnerKeyEntry, IsHex: false),
            ["Perms"] = new PdfString(PermissionsEntry, IsHex: false),
            ["P"] = new PdfInteger(PermissionFlags),
            ["EncryptMetadata"] = new PdfBoolean(EncryptMetadata)
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_fileKey);
        _disposed = true;
    }

    /// <summary>
    /// Algorithm 2.B. The revision-6 hardened hash: at least 64 rounds, the digest chosen by
    /// the modulus of the first 16 cipher bytes, terminating on the trailing byte.
    /// </summary>
    internal static byte[] HardenedHash(byte[] password, byte[] salt, byte[] userData)
    {
        var input = new byte[password.Length + salt.Length + userData.Length];
        password.CopyTo(input, 0);
        salt.CopyTo(input, password.Length);
        userData.CopyTo(input, password.Length + salt.Length);
        var k = SHA256.HashData(input);

        var round = 0;
        byte[]? e = null;
        byte[]? k1 = null;
        try
        {
            while (true)
            {
                var blockLength = password.Length + k.Length + userData.Length;
                k1 = new byte[blockLength * 64];
                for (var index = 0; index < 64; index++)
                {
                    var offset = index * blockLength;
                    password.CopyTo(k1, offset);
                    k.CopyTo(k1, offset + password.Length);
                    userData.CopyTo(k1, offset + password.Length + k.Length);
                }

                // AES-128-CBC with the first 16 bytes of K as the key and the next 16 as the IV.
                e = AesNoPaddingWithKey(k.AsSpan(0, 16).ToArray(), k.AsSpan(16, 16).ToArray(), k1);

                var modulus = 0;
                for (var index = 0; index < 16; index++)
                {
                    modulus += e[index];
                }

                var next = (modulus % 3) switch
                {
                    0 => SHA256.HashData(e),
                    1 => SHA384.HashData(e),
                    _ => SHA512.HashData(e)
                };

                round++;
                var complete = round >= 64 && e[^1] <= round - 32;
                CryptographicOperations.ZeroMemory(k);
                k = next;
                CryptographicOperations.ZeroMemory(k1);
                k1 = null;
                CryptographicOperations.ZeroMemory(e);
                e = null;
                if (complete)
                {
                    return k.AsSpan(0, HashLength).ToArray();
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(k);
            Zero(k1);
            Zero(e);
        }
    }

    /// <summary>Algorithm 10, and its verification counterpart.</summary>
    private static byte[] EncodePermissions(byte[] fileKey, int permissionFlags, bool encryptMetadata)
    {
        var block = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(block, permissionFlags);
        block[4] = 0xFF;
        block[5] = 0xFF;
        block[6] = 0xFF;
        block[7] = 0xFF;
        block[8] = encryptMetadata ? (byte)'T' : (byte)'F';
        block[9] = (byte)'a';
        block[10] = (byte)'d';
        block[11] = (byte)'b';
        RandomNumberGenerator.Fill(block.AsSpan(12, 4));

        using var aes = Aes.Create();
        aes.Key = fileKey;
        aes.Mode = CipherMode.ECB;
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    private static bool VerifyPermissions(
        byte[] fileKey,
        byte[] permissionsEntry,
        int permissionFlags,
        bool encryptMetadata)
    {
        using var aes = Aes.Create();
        aes.Key = fileKey;
        aes.Mode = CipherMode.ECB;
        byte[] block;
        try
        {
            block = aes.DecryptEcb(permissionsEntry, PaddingMode.None);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, permissionFlags);
        expected[4] = 0xFF;
        expected[5] = 0xFF;
        expected[6] = 0xFF;
        expected[7] = 0xFF;
        expected[8] = encryptMetadata ? (byte)'T' : (byte)'F';
        expected[9] = (byte)'a';
        expected[10] = (byte)'d';
        expected[11] = (byte)'b';
        return CryptographicOperations.FixedTimeEquals(block.AsSpan(0, 12), expected);
    }

    /// <summary>
    /// Revision 6 takes the password as UTF-8 after Unicode normalization, capped at 127
    /// bytes. This implements the normalization and the cap; it does not implement the full
    /// SASLprep character-prohibition tables, so a password containing prohibited or
    /// bidirectional-conflicting characters is accepted here where a strict implementation
    /// would reject it.
    /// </summary>
    internal static byte[] NormalizePassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return [];
        }

        var normalized = password.Normalize(NormalizationForm.FormKC);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        if (bytes.Length <= PasswordByteLimit)
        {
            return bytes;
        }

        // Truncation happens after encoding, as the standard requires, so the limit is on
        // bytes rather than characters.
        return bytes.AsSpan(0, PasswordByteLimit).ToArray();
    }

    private static void EnsureSupported(PdfDictionary encrypt)
    {
        if (encrypt["Filter"] is not PdfName { Value: "Standard" })
        {
            throw new NotSupportedException("Only the Standard Security Handler is supported.");
        }

        var version = ReadInteger(encrypt["V"]);
        var revision = ReadInteger(encrypt["R"]);
        if (version != Version || revision != Revision)
        {
            throw new NotSupportedException(
                $"Only AES-256 (/V 5 /R 6) is supported; this document declares /V {version} /R {revision}.");
        }

        if (ReadInteger(encrypt["Length"]) != FileKeyLength * 8)
        {
            throw new NotSupportedException("AES-256 revision 6 requires /Length 256.");
        }
    }

    private static byte[] AesNoPadding(byte[] key, byte[] data, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        var iv = new byte[16];
        return encrypt
            ? aes.EncryptCbc(data, iv, PaddingMode.None)
            : aes.DecryptCbc(data, iv, PaddingMode.None);
    }

    private static byte[] AesNoPaddingWithKey(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        return aes.EncryptCbc(data, iv, PaddingMode.None);
    }

    private static int ToPermissionFlags(PdfDocumentPermissions permissions)
    {
        // Bits 1-2 are reserved and must be 0; bits 7-8 and 13-32 must be 1.
        var value = unchecked((int)0xFFFFF0C0);
        value |= (int)permissions;
        return value;
    }

    private static PdfDocumentPermissions FromPermissionFlags(int flags) =>
        (PdfDocumentPermissions)(flags & (int)PdfDocumentPermissions.All);

    private static byte[] ReadFixedString(PdfDictionary encrypt, string key, int length)
    {
        if (encrypt[key] is not PdfString value)
        {
            throw new NotSupportedException($"The /Encrypt dictionary is missing /{key}.");
        }

        var bytes = value.GetDecodedBytes().ToArray();
        if (bytes.Length != length)
        {
            throw new NotSupportedException(
                $"The /Encrypt /{key} entry is {bytes.Length} bytes; exactly {length} are required.");
        }

        return bytes;
    }

    private static CryptFilterSelection ReadCryptFilters(PdfDictionary encrypt)
    {
        var strings = ReadCryptFilter(encrypt, "StrF");
        var streams = ReadCryptFilter(encrypt, "StmF");
        var embeddedFiles = encrypt["EFF"] is null
            ? streams
            : ReadCryptFilter(encrypt, "EFF");
        return new CryptFilterSelection(strings, streams, embeddedFiles);
    }

    private static bool ContainsExplicitCryptFilter(PdfObject? filter) => filter switch
    {
        PdfName { Value: "Crypt" } => true,
        PdfArray array => array.Items.Any(static item => item is PdfName { Value: "Crypt" }),
        _ => false
    };

    private static bool ReadCryptFilter(PdfDictionary encrypt, string selector)
    {
        var selectedName = encrypt[selector] switch
        {
            null => "Identity",
            PdfName name => name.Value,
            _ => throw new NotSupportedException($"The /Encrypt /{selector} selector is malformed.")
        };
        if (selectedName == "Identity")
        {
            return false;
        }

        if (encrypt["CF"] is not PdfDictionary filters ||
            filters[selectedName] is not PdfDictionary filter ||
            filter["CFM"] is not PdfName { Value: "AESV3" })
        {
            throw new NotSupportedException(
                $"The /Encrypt /{selector} crypt filter must resolve to /AESV3 or /Identity.");
        }

        if (filter["Length"] is { } length && ReadInteger(length) != FileKeyLength)
        {
            throw new NotSupportedException(
                $"The /Encrypt /{selector} AESV3 crypt filter must use a 32-byte key.");
        }

        if (filter["AuthEvent"] is PdfName authEvent &&
            authEvent.Value != "DocOpen" &&
            !(selector == "EFF" && authEvent.Value == "EFOpen"))
        {
            throw new NotSupportedException(
                $"The /Encrypt /{selector} crypt filter uses unsupported /AuthEvent /{authEvent.Value}.");
        }

        return true;
    }

    private static bool ArePermissionFlagsWellFormed(int flags)
    {
        const int lowReservedBits = 0b11;
        const int requiredHighBits = unchecked((int)0xFFFFF0C0);
        return (flags & lowReservedBits) == 0 &&
            (flags & requiredHighBits) == requiredHighBits;
    }

    private static void Zero(byte[]? value)
    {
        if (value is not null)
        {
            CryptographicOperations.ZeroMemory(value);
        }
    }

    private sealed record CryptFilterSelection(
        bool EncryptStrings,
        bool EncryptStreams,
        bool EncryptEmbeddedFiles);

    private static int ReadInteger(PdfObject? value) => value switch
    {
        PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
        PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
        _ => 0
    };
}
