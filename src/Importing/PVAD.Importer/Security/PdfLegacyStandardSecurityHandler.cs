using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using PVAD.Importer.Objects;

namespace PVAD.Importer.Security;

/// <summary>Opens legacy revision-3 RC4 and revision-4 AESV2 Standard Security documents.</summary>
internal sealed class PdfLegacyStandardSecurityHandler : IPdfDocumentSecurityHandler
{
    private const int KeyLength = 16;
    private static readonly byte[] PasswordPad =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    ];

    private readonly byte[] _fileKey;
    private readonly LegacyCryptMethod _stringMethod;
    private readonly LegacyCryptMethod _streamMethod;
    private readonly LegacyCryptMethod _embeddedFileMethod;
    private bool _disposed;

    private PdfLegacyStandardSecurityHandler(
        byte[] fileKey,
        LegacyCryptMethod stringMethod,
        LegacyCryptMethod streamMethod,
        LegacyCryptMethod embeddedFileMethod)
    {
        _fileKey = fileKey;
        _stringMethod = stringMethod;
        _streamMethod = streamMethod;
        _embeddedFileMethod = embeddedFileMethod;
    }

    public bool ShouldDecryptString => _stringMethod != LegacyCryptMethod.Identity;

    public static bool IsSupported(PdfDictionary? encrypt)
    {
        if (encrypt?["Filter"] is not PdfName { Value: "Standard" }) return false;
        var version = ReadInteger(encrypt["V"]);
        var revision = ReadInteger(encrypt["R"]);
        return (version == 2 && revision == 3) || (version == 4 && revision == 4);
    }

    public static PdfLegacyStandardSecurityHandler Open(
        PdfDictionary encrypt,
        ReadOnlySpan<byte> documentId,
        string password,
        out PdfDecryptionResult result)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        if (!IsSupported(encrypt))
        {
            throw new NotSupportedException("Only legacy RC4 /V 2 /R 3 and AES-128 /V 4 /R 4 are supported here.");
        }

        if (documentId.IsEmpty)
        {
            throw new InvalidDataException("A legacy encrypted PDF requires the first trailer /ID value.");
        }

        var ownerEntry = ReadFixedString(encrypt, "O", 32);
        var userEntry = ReadFixedString(encrypt, "U", 32);
        var permissionFlags = ReadInteger(encrypt["P"]);
        var encryptMetadata = encrypt["EncryptMetadata"] is not PdfBoolean { Value: false };
        var methods = ReadCryptMethods(encrypt);
        var passwordPad = PadPassword(password);
        byte[]? fileKey = null;
        byte[]? recoveredUserPad = null;
        try
        {
            recoveredUserPad = RecoverUserPasswordPad(passwordPad, ownerEntry);
            fileKey = ComputeFileKey(
                recoveredUserPad, ownerEntry, permissionFlags, documentId, encryptMetadata);
            var passwordKind = IsUserEntryValid(fileKey, documentId, userEntry)
                ? PdfPasswordKind.Owner
                : PdfPasswordKind.None;

            if (passwordKind == PdfPasswordKind.None)
            {
                CryptographicOperations.ZeroMemory(fileKey);
                fileKey = ComputeFileKey(
                    passwordPad, ownerEntry, permissionFlags, documentId, encryptMetadata);
                if (!IsUserEntryValid(fileKey, documentId, userEntry))
                {
                    throw new PdfPasswordException("The supplied password does not open this document.");
                }

                passwordKind = PdfPasswordKind.User;
            }

            result = new PdfDecryptionResult(
                passwordKind,
                (PdfDocumentPermissions)(permissionFlags & (int)PdfDocumentPermissions.All),
                encryptMetadata,
                PermissionsVerified: false);
            return new PdfLegacyStandardSecurityHandler(
                fileKey, methods.Strings, methods.Streams, methods.EmbeddedFiles);
        }
        catch
        {
            Zero(fileKey);
            throw;
        }
        finally
        {
            Zero(passwordPad);
            Zero(recoveredUserPad);
        }
    }

    public bool ShouldDecryptStream(PdfDictionary dictionary)
    {
        if (dictionary["Type"] is PdfName { Value: "XRef" }) return false;
        if (ContainsExplicitCryptFilter(dictionary["Filter"]))
        {
            throw new NotSupportedException("Streams with an explicit /Crypt filter are not supported yet.");
        }

        var method = dictionary["Type"] is PdfName { Value: "EmbeddedFile" }
            ? _embeddedFileMethod
            : _streamMethod;
        return method != LegacyCryptMethod.Identity;
    }

    public byte[] DecryptValue(ReadOnlySpan<byte> ciphertext, int objectNumber, int generation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var method = _stringMethod;
        return Decrypt(ciphertext, objectNumber, generation, method);
    }

    internal byte[] DecryptStreamValue(
        ReadOnlySpan<byte> ciphertext,
        int objectNumber,
        int generation,
        PdfDictionary dictionary)
    {
        var method = dictionary["Type"] is PdfName { Value: "EmbeddedFile" }
            ? _embeddedFileMethod
            : _streamMethod;
        return Decrypt(ciphertext, objectNumber, generation, method);
    }

    private byte[] Decrypt(
        ReadOnlySpan<byte> ciphertext,
        int objectNumber,
        int generation,
        LegacyCryptMethod method)
    {
        if (method == LegacyCryptMethod.Identity) return ciphertext.ToArray();
        var key = GetObjectKey(objectNumber, generation, method == LegacyCryptMethod.Aes128);
        try
        {
            if (method == LegacyCryptMethod.Rc4) return Rc4Transform(key, ciphertext);
            if (ciphertext.Length < 32 || (ciphertext.Length - 16) % 16 != 0)
            {
                throw new PdfPasswordException("An encrypted value is not a well-formed AES-128 block.");
            }

            using var aes = Aes.Create();
            aes.Key = key;
            return aes.DecryptCbc(ciphertext[16..], ciphertext[..16], PaddingMode.PKCS7);
        }
        finally
        {
            Zero(key);
        }
    }

    private byte[] GetObjectKey(int objectNumber, int generation, bool aes)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(_fileKey);
        md5.AppendData(
        [
            (byte)objectNumber,
            (byte)(objectNumber >> 8),
            (byte)(objectNumber >> 16),
            (byte)generation,
            (byte)(generation >> 8)
        ]);
        if (aes) md5.AppendData([0x73, 0x41, 0x6C, 0x54]);
        var digest = md5.GetHashAndReset();
        var length = Math.Min(_fileKey.Length + 5, 16);
        return digest.AsSpan(0, length).ToArray();
    }

    private static byte[] RecoverUserPasswordPad(byte[] ownerPasswordPad, byte[] ownerEntry)
    {
        var digest = MD5.HashData(ownerPasswordPad);
        for (var round = 0; round < 50; round++)
        {
            var next = MD5.HashData(digest.AsSpan(0, KeyLength));
            Zero(digest);
            digest = next;
        }
        var key = digest.AsSpan(0, KeyLength).ToArray();
        var value = ownerEntry.ToArray();
        for (var round = 19; round >= 0; round--)
        {
            var roundKey = XorKey(key, round);
            var next = Rc4Transform(roundKey, value);
            Zero(roundKey);
            Zero(value);
            value = next;
        }

        Zero(key);
        Zero(digest);
        return value;
    }

    private static byte[] ComputeFileKey(
        byte[] passwordPad,
        byte[] ownerEntry,
        int permissions,
        ReadOnlySpan<byte> documentId,
        bool encryptMetadata)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(passwordPad);
        md5.AppendData(ownerEntry);
        Span<byte> permissionBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(permissionBytes, permissions);
        md5.AppendData(permissionBytes);
        md5.AppendData(documentId);
        if (!encryptMetadata) md5.AppendData([0xFF, 0xFF, 0xFF, 0xFF]);
        var digest = md5.GetHashAndReset();
        for (var round = 0; round < 50; round++)
        {
            var next = MD5.HashData(digest.AsSpan(0, KeyLength));
            Zero(digest);
            digest = next;
        }
        var key = digest.AsSpan(0, KeyLength).ToArray();
        Zero(digest);
        return key;
    }

    private static bool IsUserEntryValid(byte[] fileKey, ReadOnlySpan<byte> documentId, byte[] userEntry)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(PasswordPad);
        md5.AppendData(documentId);
        var hash = md5.GetHashAndReset();
        var expected = Rc4Transform(fileKey, hash);
        Zero(hash);
        for (var round = 1; round <= 19; round++)
        {
            var roundKey = XorKey(fileKey, round);
            var next = Rc4Transform(roundKey, expected);
            Zero(roundKey);
            Zero(expected);
            expected = next;
        }

        var valid = CryptographicOperations.FixedTimeEquals(expected, userEntry.AsSpan(0, 16));
        Zero(expected);
        return valid;
    }

    private static LegacyMethods ReadCryptMethods(PdfDictionary encrypt)
    {
        if (ReadInteger(encrypt["V"]) == 2)
        {
            return new LegacyMethods(LegacyCryptMethod.Rc4, LegacyCryptMethod.Rc4, LegacyCryptMethod.Rc4);
        }

        var strings = ReadCryptMethod(encrypt, "StrF");
        var streams = ReadCryptMethod(encrypt, "StmF");
        var embedded = encrypt["EFF"] is null ? streams : ReadCryptMethod(encrypt, "EFF");
        return new LegacyMethods(strings, streams, embedded);
    }

    private static LegacyCryptMethod ReadCryptMethod(PdfDictionary encrypt, string selector)
    {
        var selected = encrypt[selector] switch
        {
            null => "Identity",
            PdfName name => name.Value,
            _ => throw new NotSupportedException($"The /Encrypt /{selector} selector is malformed.")
        };
        if (selected == "Identity") return LegacyCryptMethod.Identity;
        if (encrypt["CF"] is not PdfDictionary filters || filters[selected] is not PdfDictionary filter)
        {
            throw new NotSupportedException($"The /Encrypt /{selector} crypt filter is missing.");
        }

        return filter["CFM"] switch
        {
            PdfName { Value: "AESV2" } => LegacyCryptMethod.Aes128,
            PdfName { Value: "V2" } => LegacyCryptMethod.Rc4,
            _ => throw new NotSupportedException($"The /Encrypt /{selector} crypt filter is unsupported.")
        };
    }

    private static byte[] PadPassword(string? password)
    {
        var bytes = string.IsNullOrEmpty(password) ? [] : Encoding.GetEncoding("ISO-8859-1").GetBytes(password);
        var result = new byte[32];
        var count = Math.Min(bytes.Length, result.Length);
        bytes.AsSpan(0, count).CopyTo(result);
        PasswordPad.AsSpan(0, result.Length - count).CopyTo(result.AsSpan(count));
        Zero(bytes);
        return result;
    }

    private static byte[] ReadFixedString(PdfDictionary dictionary, string key, int length)
    {
        if (dictionary[key] is not PdfString value || value.GetDecodedBytes().Length != length)
        {
            throw new NotSupportedException($"The /Encrypt /{key} entry must contain exactly {length} bytes.");
        }

        return value.GetDecodedBytes().ToArray();
    }

    private static byte[] Rc4Transform(byte[] key, ReadOnlySpan<byte> input)
    {
        var state = Enumerable.Range(0, 256).Select(static value => (byte)value).ToArray();
        var j = 0;
        for (var index = 0; index < state.Length; index++)
        {
            j = (j + state[index] + key[index % key.Length]) & 255;
            (state[index], state[j]) = (state[j], state[index]);
        }

        var output = new byte[input.Length];
        var i = 0;
        j = 0;
        for (var index = 0; index < input.Length; index++)
        {
            i = (i + 1) & 255;
            j = (j + state[i]) & 255;
            (state[i], state[j]) = (state[j], state[i]);
            output[index] = (byte)(input[index] ^ state[(state[i] + state[j]) & 255]);
        }

        Zero(state);
        return output;
    }

    private static byte[] XorKey(byte[] key, int value)
    {
        var result = new byte[key.Length];
        for (var index = 0; index < key.Length; index++) result[index] = (byte)(key[index] ^ value);
        return result;
    }

    private static bool ContainsExplicitCryptFilter(PdfObject? filter) => filter switch
    {
        PdfName { Value: "Crypt" } => true,
        PdfArray array => array.Items.Any(static item => item is PdfName { Value: "Crypt" }),
        _ => false
    };

    private static int ReadInteger(PdfObject? value) => value switch
    {
        PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
        PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
        _ => 0
    };

    private static void Zero(byte[]? value)
    {
        if (value is not null) CryptographicOperations.ZeroMemory(value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Zero(_fileKey);
        _disposed = true;
    }

    private enum LegacyCryptMethod { Identity, Rc4, Aes128 }
    private sealed record LegacyMethods(
        LegacyCryptMethod Strings,
        LegacyCryptMethod Streams,
        LegacyCryptMethod EmbeddedFiles);
}
