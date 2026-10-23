using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PVAD.Pdf.Serialization.Security;

/// <summary>Writes the PDF 1.6 revision-4 Standard Security Handler with AESV2.</summary>
internal sealed class Aes128SecurityHandler : IPdfSecurityHandler
{
    private const int KeyLength = 16;
    private static readonly byte[] PasswordPad =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    ];
    private static readonly byte[] StreamMarker = "stream\n"u8.ToArray();
    private static readonly byte[] LengthMarker = "/Length "u8.ToArray();

    private readonly byte[] _fileKey;
    private bool _disposed;

    private Aes128SecurityHandler(
        byte[] fileKey,
        byte[] ownerEntry,
        byte[] userEntry,
        int permissions,
        bool encryptMetadata,
        byte[] documentId)
    {
        _fileKey = fileKey;
        OwnerEntry = ownerEntry;
        UserEntry = userEntry;
        Permissions = permissions;
        EncryptMetadata = encryptMetadata;
        DocumentId = documentId;
    }

    public byte[] OwnerEntry { get; }
    public byte[] UserEntry { get; }
    public int Permissions { get; }
    public bool EncryptMetadata { get; }
    public byte[] DocumentId { get; }
    public string PdfVersion => "1.6";

    public static Aes128SecurityHandler Create(PdfEncryptionOptions options, byte[] documentId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(documentId);
        if (options.Algorithm != PdfEncryptionAlgorithm.Aes128)
        {
            throw new NotSupportedException("The AES-128 handler supports only PdfEncryptionAlgorithm.Aes128.");
        }

        if (options.Permissions != PdfPermissions.All &&
            (string.IsNullOrEmpty(options.OwnerPassword) || options.OwnerPassword == options.UserPassword))
        {
            throw new ArgumentException(
                "Restricted PDF permissions require an owner password distinct from the user password.",
                nameof(options));
        }

        var userPad = PadPassword(options.UserPassword);
        var ownerPad = PadPassword(options.OwnerPassword ?? options.UserPassword);
        byte[]? ownerEntry = null;
        byte[]? fileKey = null;
        try
        {
            var permissions = ComputePermissionValue(options.Permissions);
            ownerEntry = ComputeOwnerEntry(ownerPad, userPad);
            fileKey = ComputeFileKey(
                userPad, ownerEntry, permissions, documentId, options.EncryptMetadata);
            var userEntry = ComputeUserEntry(fileKey, documentId);
            return new Aes128SecurityHandler(
                fileKey, ownerEntry, userEntry, permissions, options.EncryptMetadata, documentId);
        }
        catch
        {
            Zero(ownerEntry);
            Zero(fileKey);
            throw;
        }
        finally
        {
            Zero(userPad);
            Zero(ownerPad);
        }
    }

    public byte[] EncryptObjectBody(int objectNumber, int generation, byte[] body)
        => EncryptObjectBody(_fileKey, EncryptMetadata, objectNumber, generation, body);

    internal static byte[] EncryptObjectBody(
        byte[] fileKey,
        bool encryptMetadata,
        int objectNumber,
        int generation,
        byte[] body)
    {
        var objectKey = GetObjectKey(fileKey, objectNumber, generation);
        try
        {
            var streamStart = IndexOf(body, StreamMarker);
            if (streamStart < 0)
            {
                return EncryptLiteralStrings(body, objectKey);
            }

            var payloadStart = streamStart + StreamMarker.Length;
            var (_, _, payloadLength) = ParseStreamLength(body);
            if (payloadLength < 0 || payloadStart + payloadLength > body.Length)
            {
                throw new InvalidDataException("A generated PDF stream has an invalid direct /Length value.");
            }

            var metadata = !encryptMetadata &&
                body.AsSpan(0, streamStart).IndexOf("/Type /Metadata"u8) >= 0;
            var encryptedPayload = metadata
                ? body.AsSpan(payloadStart, payloadLength).ToArray()
                : EncryptAes(body.AsSpan(payloadStart, payloadLength), objectKey);
            var prefix = EncryptLiteralStrings(body.AsSpan(0, streamStart).ToArray(), objectKey);
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
        finally
        {
            Zero(objectKey);
        }
    }

    public byte[] BuildEncryptDictionary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes(
            $"<< /Filter /Standard /V 4 /R 4 /Length 128 /P {Permissions} " +
            "/CF << /StdCF << /CFM /AESV2 /AuthEvent /DocOpen /Length 16 >> >> " +
            "/StmF /StdCF /StrF /StdCF "));
        WriteHexEntry(output, "O", OwnerEntry);
        WriteHexEntry(output, "U", UserEntry);
        output.Write(Encoding.ASCII.GetBytes($"/EncryptMetadata {(EncryptMetadata ? "true" : "false")} >>\n"));
        return output.ToArray();
    }

    private static byte[] GetObjectKey(byte[] fileKey, int objectNumber, int generation)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(fileKey);
        md5.AppendData(
        [
            (byte)objectNumber,
            (byte)(objectNumber >> 8),
            (byte)(objectNumber >> 16),
            (byte)generation,
            (byte)(generation >> 8),
            0x73, 0x41, 0x6C, 0x54
        ]);
        return md5.GetHashAndReset().AsSpan(0, 16).ToArray();
    }

    private static byte[] ComputeOwnerEntry(byte[] ownerPad, byte[] userPad)
    {
        var digest = MD5.HashData(ownerPad);
        for (var round = 0; round < 50; round++)
        {
            var next = MD5.HashData(digest.AsSpan(0, KeyLength));
            Zero(digest);
            digest = next;
        }

        var key = digest.AsSpan(0, KeyLength).ToArray();
        Zero(digest);
        var result = Rc4.Transform(key, userPad);
        for (var round = 1; round <= 19; round++)
        {
            var roundKey = XorKey(key, round);
            var next = Rc4.Transform(roundKey, result);
            Zero(roundKey);
            Zero(result);
            result = next;
        }

        Zero(key);
        return result;
    }

    private static byte[] ComputeFileKey(
        byte[] userPad,
        byte[] ownerEntry,
        int permissions,
        byte[] documentId,
        bool encryptMetadata)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(userPad);
        md5.AppendData(ownerEntry);
        Span<byte> permissionBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(permissionBytes, permissions);
        md5.AppendData(permissionBytes);
        md5.AppendData(documentId);
        if (!encryptMetadata)
        {
            md5.AppendData([0xFF, 0xFF, 0xFF, 0xFF]);
        }

        var digest = md5.GetHashAndReset();
        for (var round = 0; round < 50; round++)
        {
            var next = MD5.HashData(digest.AsSpan(0, KeyLength));
            Zero(digest);
            digest = next;
        }

        return digest.AsSpan(0, KeyLength).ToArray();
    }

    private static byte[] ComputeUserEntry(byte[] fileKey, byte[] documentId)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(PasswordPad);
        md5.AppendData(documentId);
        var hash = md5.GetHashAndReset();
        var result = Rc4.Transform(fileKey, hash);
        Zero(hash);
        for (var round = 1; round <= 19; round++)
        {
            var roundKey = XorKey(fileKey, round);
            var next = Rc4.Transform(roundKey, result);
            Zero(roundKey);
            Zero(result);
            result = next;
        }

        var entry = new byte[32];
        result.CopyTo(entry, 0);
        RandomNumberGenerator.Fill(entry.AsSpan(16));
        Zero(result);
        return entry;
    }

    private static byte[] EncryptLiteralStrings(byte[] body, byte[] key)
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
            var encrypted = EncryptAes(raw, key);
            output.WriteByte((byte)'<');
            output.Write(Encoding.ASCII.GetBytes(Convert.ToHexString(encrypted)));
            output.WriteByte((byte)'>');
            Zero(raw);
            Zero(encrypted);
        }

        return output.ToArray();
    }

    private static byte[] EncryptAes(ReadOnlySpan<byte> plaintext, byte[] key)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = key;
        var ciphertext = aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);
        var result = new byte[iv.Length + ciphertext.Length];
        iv.CopyTo(result, 0);
        ciphertext.CopyTo(result, iv.Length);
        Zero(iv);
        Zero(ciphertext);
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
                escaped.Add(body[index++]);
                continue;
            }

            if (value == (byte)'(') depth++;
            if (value == (byte)')' && --depth == 0) break;
            escaped.Add(value);
        }

        if (depth != 0) throw new InvalidDataException("A generated PDF object contains an unterminated literal string.");
        return escaped.ToArray();
    }

    private static (int Start, int End, int Value) ParseStreamLength(byte[] body)
    {
        var marker = IndexOf(body, LengthMarker);
        if (marker < 0) return (-1, -1, -1);
        var start = marker + LengthMarker.Length;
        var end = start;
        var value = 0;
        while (end < body.Length && body[end] is >= (byte)'0' and <= (byte)'9')
        {
            value = checked(value * 10 + body[end++] - (byte)'0');
        }

        return end == start ? (-1, -1, -1) : (start, end, value);
    }

    private static byte[] ReplaceLength(byte[] prefix, int length)
    {
        var (start, end, _) = ParseStreamLength(prefix);
        if (start < 0) throw new InvalidDataException("A generated PDF stream is missing a direct /Length value.");
        var replacement = Encoding.ASCII.GetBytes(length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var result = new byte[prefix.Length - (end - start) + replacement.Length];
        prefix.AsSpan(0, start).CopyTo(result);
        replacement.CopyTo(result, start);
        prefix.AsSpan(end).CopyTo(result.AsSpan(start + replacement.Length));
        return result;
    }

    private static byte[] PadPassword(string? password)
    {
        var bytes = string.IsNullOrEmpty(password)
            ? []
            : Encoding.GetEncoding("ISO-8859-1").GetBytes(password);
        var padded = new byte[32];
        var count = Math.Min(bytes.Length, padded.Length);
        bytes.AsSpan(0, count).CopyTo(padded);
        PasswordPad.AsSpan(0, padded.Length - count).CopyTo(padded.AsSpan(count));
        Zero(bytes);
        return padded;
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

    private static byte[] XorKey(byte[] key, int round)
    {
        var result = new byte[key.Length];
        for (var index = 0; index < key.Length; index++) result[index] = (byte)(key[index] ^ round);
        return result;
    }

    private static int IndexOf(byte[] source, byte[] value)
    {
        for (var index = 0; index <= source.Length - value.Length; index++)
        {
            if (source.AsSpan(index, value.Length).SequenceEqual(value)) return index;
        }

        return -1;
    }

    private static void WriteHexEntry(Stream output, string name, byte[] value) =>
        output.Write(Encoding.ASCII.GetBytes($"/{name} <{Convert.ToHexString(value)}> "));

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
}
