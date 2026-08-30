using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PXA.Pdf.Serialization.Security;

/// <summary>Writes PDF 2.0 Standard Security Handler revision 6 documents.</summary>
internal sealed class Aes256SecurityHandler : IPdfSecurityHandler
{
    private const int FileKeyLength = 32;
    private const int HashLength = 32;
    private const int SaltLength = 8;
    private static readonly byte[] StreamMarker = Encoding.ASCII.GetBytes("stream\n");
    private static readonly byte[] LengthMarker = Encoding.ASCII.GetBytes("/Length ");

    private readonly byte[] _fileKey;
    private bool _disposed;

    private Aes256SecurityHandler(
        byte[] fileKey,
        byte[] ownerEntry,
        byte[] userEntry,
        byte[] ownerKeyEntry,
        byte[] userKeyEntry,
        byte[] permissionsEntry,
        int permissions,
        bool encryptMetadata,
        byte[] documentId)
    {
        _fileKey = fileKey;
        OwnerEntry = ownerEntry;
        UserEntry = userEntry;
        OwnerKeyEntry = ownerKeyEntry;
        UserKeyEntry = userKeyEntry;
        PermissionsEntry = permissionsEntry;
        Permissions = permissions;
        EncryptMetadata = encryptMetadata;
        DocumentId = documentId;
    }

    public byte[] OwnerEntry { get; }
    public byte[] UserEntry { get; }
    public byte[] OwnerKeyEntry { get; }
    public byte[] UserKeyEntry { get; }
    public byte[] PermissionsEntry { get; }
    public int Permissions { get; }
    public bool EncryptMetadata { get; }
    public byte[] DocumentId { get; }
    public string PdfVersion => "2.0";

    public static Aes256SecurityHandler Create(PdfEncryptionOptions options, byte[] documentId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(documentId);
        if (options.Algorithm != PdfEncryptionAlgorithm.Aes256)
        {
            throw new NotSupportedException("The PDF 2.0 handler supports only PdfEncryptionAlgorithm.Aes256.");
        }

        var userPassword = PdfPasswordPreparation.Prepare(options.UserPassword);
        var ownerPassword = PdfPasswordPreparation.Prepare(options.OwnerPassword ?? options.UserPassword);
        if (options.Permissions != PdfPermissions.All &&
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
            var userValidationSalt = RandomNumberGenerator.GetBytes(SaltLength);
            var userKeySalt = RandomNumberGenerator.GetBytes(SaltLength);
            var userEntry = new byte[48];
            userValidationHash = HardenedHash(userPassword, userValidationSalt, []);
            userValidationHash.CopyTo(userEntry, 0);
            userValidationSalt.CopyTo(userEntry, HashLength);
            userKeySalt.CopyTo(userEntry, HashLength + SaltLength);
            userKeyHash = HardenedHash(userPassword, userKeySalt, []);
            var userKeyEntry = AesNoPadding(userKeyHash, fileKey);

            var ownerValidationSalt = RandomNumberGenerator.GetBytes(SaltLength);
            var ownerKeySalt = RandomNumberGenerator.GetBytes(SaltLength);
            var ownerEntry = new byte[48];
            ownerValidationHash = HardenedHash(ownerPassword, ownerValidationSalt, userEntry);
            ownerValidationHash.CopyTo(ownerEntry, 0);
            ownerValidationSalt.CopyTo(ownerEntry, HashLength);
            ownerKeySalt.CopyTo(ownerEntry, HashLength + SaltLength);
            ownerKeyHash = HardenedHash(ownerPassword, ownerKeySalt, userEntry);
            var ownerKeyEntry = AesNoPadding(ownerKeyHash, fileKey);

            var permissions = ComputePermissionValue(options.Permissions);
            var permissionsEntry = EncodePermissions(fileKey, permissions, options.EncryptMetadata);
            return new Aes256SecurityHandler(
                fileKey, ownerEntry, userEntry, ownerKeyEntry, userKeyEntry,
                permissionsEntry, permissions, options.EncryptMetadata, documentId);
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

    public byte[] EncryptObjectBody(int objectNumber, int generation, byte[] body)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var streamStart = IndexOf(body, StreamMarker, 0);
        if (streamStart < 0)
        {
            return EncryptLiteralStrings(body);
        }

        var payloadStart = streamStart + StreamMarker.Length;
        var (_, _, payloadLength) = ParseStreamLength(body);
        if (payloadLength < 0 || payloadStart + payloadLength > body.Length)
        {
            throw new InvalidDataException("A generated PDF stream has an invalid direct /Length value.");
        }

        var isUnencryptedMetadata = !EncryptMetadata &&
            body.AsSpan(0, streamStart).IndexOf("/Type /Metadata"u8) >= 0;
        var encryptedPayload = isUnencryptedMetadata
            ? body.AsSpan(payloadStart, payloadLength).ToArray()
            : EncryptValue(body.AsSpan(payloadStart, payloadLength));
        var prefix = EncryptLiteralStrings(body.AsSpan(0, streamStart).ToArray());
        var rewrittenPrefix = ReplaceLength(prefix, encryptedPayload.Length);
        var suffixStart = payloadStart + payloadLength;
        using var output = new MemoryStream(
            rewrittenPrefix.Length + StreamMarker.Length + encryptedPayload.Length + body.Length - suffixStart);
        output.Write(rewrittenPrefix);
        output.Write(StreamMarker);
        output.Write(encryptedPayload);
        output.Write(body.AsSpan(suffixStart));
        return output.ToArray();
    }

    public byte[] BuildEncryptDictionary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var prefix = Encoding.ASCII.GetBytes(
            $"<< /Filter /Standard /V 5 /R 6 /Length 256 /P {Permissions} " +
            "/CF << /StdCF << /CFM /AESV3 /AuthEvent /DocOpen /Length 32 >> >> " +
            "/StmF /StdCF /StrF /StdCF ");
        using var output = new MemoryStream();
        output.Write(prefix);
        WriteHexEntry(output, "O", OwnerEntry);
        WriteHexEntry(output, "OE", OwnerKeyEntry);
        WriteHexEntry(output, "U", UserEntry);
        WriteHexEntry(output, "UE", UserKeyEntry);
        WriteHexEntry(output, "Perms", PermissionsEntry);
        output.Write(Encoding.ASCII.GetBytes($"/EncryptMetadata {(EncryptMetadata ? "true" : "false")} >>\n"));
        return output.ToArray();
    }

    private byte[] EncryptLiteralStrings(byte[] body)
    {
        using var output = new MemoryStream(body.Length);
        var index = 0;
        while (index < body.Length)
        {
            if (body[index] != (byte)'(')
            {
                output.WriteByte(body[index++]);
                continue;
            }

            var raw = ReadLiteralString(body, ref index);
            var encrypted = EncryptValue(raw);
            output.WriteByte((byte)'<');
            output.Write(Encoding.ASCII.GetBytes(Convert.ToHexString(encrypted)));
            output.WriteByte((byte)'>');
        }

        return output.ToArray();
    }

    private byte[] EncryptValue(ReadOnlySpan<byte> plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = _fileKey;
        var ciphertext = aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);
        var result = new byte[iv.Length + ciphertext.Length];
        iv.CopyTo(result, 0);
        ciphertext.CopyTo(result, iv.Length);
        return result;
    }

    private static byte[] ReadLiteralString(byte[] body, ref int index)
    {
        index++;
        var escaped = new List<byte>();
        var depth = 1;
        while (index < body.Length && depth > 0)
        {
            var value = body[index++];
            if (value == (byte)'\\' && index < body.Length)
            {
                escaped.Add(value);
                escaped.Add(body[index++]);
                continue;
            }

            if (value == (byte)'(')
            {
                depth++;
            }
            else if (value == (byte)')')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }
            }

            escaped.Add(value);
        }

        if (depth != 0)
        {
            throw new InvalidDataException("A generated PDF object contains an unterminated literal string.");
        }

        return Unescape(escaped);
    }

    private static byte[] Unescape(List<byte> escaped)
    {
        var output = new List<byte>(escaped.Count);
        for (var index = 0; index < escaped.Count; index++)
        {
            if (escaped[index] == (byte)'\\' && index + 1 < escaped.Count)
            {
                output.Add(escaped[++index]);
            }
            else
            {
                output.Add(escaped[index]);
            }
        }

        return output.ToArray();
    }

    private static (int Start, int End, int Value) ParseStreamLength(byte[] body)
    {
        var marker = IndexOf(body, LengthMarker, 0);
        if (marker < 0)
        {
            return (-1, -1, -1);
        }

        var start = marker + LengthMarker.Length;
        var end = start;
        var value = 0;
        while (end < body.Length && body[end] is >= (byte)'0' and <= (byte)'9')
        {
            value = checked(value * 10 + body[end] - (byte)'0');
            end++;
        }

        return end == start ? (-1, -1, -1) : (start, end, value);
    }

    private static byte[] ReplaceLength(byte[] prefix, int length)
    {
        var (start, end, _) = ParseStreamLength(prefix);
        if (start < 0)
        {
            throw new InvalidDataException("A generated PDF stream is missing a direct /Length value.");
        }

        var replacement = Encoding.ASCII.GetBytes(length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var result = new byte[prefix.Length - (end - start) + replacement.Length];
        prefix.AsSpan(0, start).CopyTo(result);
        replacement.CopyTo(result, start);
        prefix.AsSpan(end).CopyTo(result.AsSpan(start + replacement.Length));
        return result;
    }

    private static byte[] HardenedHash(byte[] password, byte[] salt, byte[] userData)
    {
        var input = new byte[password.Length + salt.Length + userData.Length];
        password.CopyTo(input, 0);
        salt.CopyTo(input, password.Length);
        userData.CopyTo(input, password.Length + salt.Length);
        var hash = SHA256.HashData(input);
        byte[]? encrypted = null;
        byte[]? repeated = null;
        try
        {
            for (var round = 0; ; round++)
            {
                var blockLength = password.Length + hash.Length + userData.Length;
                repeated = new byte[blockLength * 64];
                for (var copy = 0; copy < 64; copy++)
                {
                    var offset = copy * blockLength;
                    password.CopyTo(repeated, offset);
                    hash.CopyTo(repeated, offset + password.Length);
                    userData.CopyTo(repeated, offset + password.Length + hash.Length);
                }

                encrypted = EncryptNoPadding(
                    hash.AsSpan(0, 16).ToArray(), hash.AsSpan(16, 16).ToArray(), repeated);
                var selector = 0;
                for (var index = 0; index < 16; index++) selector += encrypted[index];
                var next = (selector % 3) switch
                {
                    0 => SHA256.HashData(encrypted),
                    1 => SHA384.HashData(encrypted),
                    _ => SHA512.HashData(encrypted)
                };
                var complete = round + 1 >= 64 && encrypted[^1] <= round + 1 - 32;
                CryptographicOperations.ZeroMemory(hash);
                hash = next;
                Zero(repeated);
                repeated = null;
                Zero(encrypted);
                encrypted = null;
                if (complete) return hash.AsSpan(0, HashLength).ToArray();
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(hash);
            Zero(repeated);
            Zero(encrypted);
        }
    }

    private static byte[] AesNoPadding(byte[] key, byte[] data) =>
        EncryptNoPadding(key, new byte[16], data);

    private static byte[] EncryptNoPadding(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(data, iv, PaddingMode.None);
    }

    private static byte[] EncodePermissions(byte[] fileKey, int permissions, bool encryptMetadata)
    {
        var block = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(block, permissions);
        block.AsSpan(4, 4).Fill(0xFF);
        block[8] = encryptMetadata ? (byte)'T' : (byte)'F';
        block[9] = (byte)'a';
        block[10] = (byte)'d';
        block[11] = (byte)'b';
        RandomNumberGenerator.Fill(block.AsSpan(12, 4));
        using var aes = Aes.Create();
        aes.Key = fileKey;
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    private static int ComputePermissionValue(PdfPermissions permissions)
    {
        var value = unchecked((int)0xFFFFF0C0);
        if (permissions.HasFlag(PdfPermissions.Print)) value |= 4;
        if (permissions.HasFlag(PdfPermissions.Modify)) value |= 8;
        if (permissions.HasFlag(PdfPermissions.Copy)) value |= 16;
        if (permissions.HasFlag(PdfPermissions.AnnotateAndFillForms)) value |= 32;
        if (permissions.HasFlag(PdfPermissions.FillForms)) value |= 256;
        if (permissions.HasFlag(PdfPermissions.ExtractForAccessibility)) value |= 512;
        if (permissions.HasFlag(PdfPermissions.Assemble)) value |= 1024;
        if (permissions.HasFlag(PdfPermissions.PrintHighResolution)) value |= 2048;
        return value;
    }

    private static void WriteHexEntry(Stream output, string name, byte[] value) =>
        output.Write(Encoding.ASCII.GetBytes($"/{name} <{Convert.ToHexString(value)}> "));

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var index = start; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle)) return index;
        }

        return -1;
    }

    private static void Zero(byte[]? value)
    {
        if (value is not null) CryptographicOperations.ZeroMemory(value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        CryptographicOperations.ZeroMemory(_fileKey);
        _disposed = true;
    }
}
