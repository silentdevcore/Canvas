using System.Security.Cryptography;
using System.Text;

namespace PXA.Pdf.Serialization.Security;

/// <summary>
/// Implements the PDF Standard Security Handler (PDF 32000-1, §7.6.3) for RC4-128
/// (<c>/V 2 /R 3</c>). Computes the file key, <c>/O</c> and <c>/U</c> entries, derives per-object
/// keys (Algorithm 1), and encrypts the strings and streams inside each indirect object.
/// AES-128 (<c>/V 4 /R 4</c>) is reserved for a future revision.
/// </summary>
internal sealed class StandardSecurityHandler
{
    private const int Revision = 3;
    private const int Version = 2;
    private const int KeyLengthBytes = 16; // 128-bit

    // PDF password padding string (PDF 32000-1, Table 19 / Algorithm 2, step (a)).
    private static readonly byte[] PasswordPad =
    {
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    };

    private readonly byte[] _encryptionKey;

    private StandardSecurityHandler(byte[] encryptionKey, byte[] o, byte[] u, int permissions, byte[] documentId)
    {
        _encryptionKey = encryptionKey;
        OwnerEntry = o;
        UserEntry = u;
        Permissions = permissions;
        DocumentId = documentId;
    }

    public byte[] OwnerEntry { get; }

    public byte[] UserEntry { get; }

    public int Permissions { get; }

    public byte[] DocumentId { get; }

    public static StandardSecurityHandler Create(PdfEncryptionOptions options, byte[] documentId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(documentId);

        if (options.Algorithm == PdfEncryptionAlgorithm.Aes128)
        {
            throw new NotSupportedException(
                "AES-128 encryption is not yet implemented. Use PdfEncryptionAlgorithm.Rc4_128.");
        }

        var userPad = PadPassword(options.UserPassword);
        var ownerSource = string.IsNullOrEmpty(options.OwnerPassword) ? options.UserPassword : options.OwnerPassword;
        var ownerPad = PadPassword(ownerSource);
        var permissions = ComputePermissionValue(options.Permissions);

        var o = ComputeOwnerEntry(ownerPad, userPad);
        var key = ComputeEncryptionKey(userPad, o, permissions, documentId);
        var u = ComputeUserEntry(key, documentId);

        return new StandardSecurityHandler(key, o, u, permissions, documentId);
    }

    // Algorithm 3: compute the /O entry.
    private static byte[] ComputeOwnerEntry(byte[] ownerPad, byte[] userPad)
    {
        var digest = MD5.HashData(ownerPad);
        for (var i = 0; i < 50; i++)
        {
            digest = MD5.HashData(digest.AsSpan(0, KeyLengthBytes).ToArray());
        }

        var rc4Key = digest.AsSpan(0, KeyLengthBytes).ToArray();
        var result = Rc4.Transform(rc4Key, userPad);
        for (var i = 1; i <= 19; i++)
        {
            result = Rc4.Transform(XorKey(rc4Key, i), result);
        }

        return result; // 32 bytes
    }

    // Algorithm 2: compute the file encryption key.
    private static byte[] ComputeEncryptionKey(byte[] userPad, byte[] owner, int permissions, byte[] documentId)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(userPad);
        md5.AppendData(owner);
        md5.AppendData(new[]
        {
            (byte)(permissions & 0xFF),
            (byte)((permissions >> 8) & 0xFF),
            (byte)((permissions >> 16) & 0xFF),
            (byte)((permissions >> 24) & 0xFF)
        });
        md5.AppendData(documentId);

        var digest = md5.GetHashAndReset();
        for (var i = 0; i < 50; i++)
        {
            digest = MD5.HashData(digest.AsSpan(0, KeyLengthBytes).ToArray());
        }

        return digest.AsSpan(0, KeyLengthBytes).ToArray();
    }

    // Algorithm 5: compute the /U entry (revision 3+).
    private static byte[] ComputeUserEntry(byte[] encryptionKey, byte[] documentId)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(PasswordPad);
        md5.AppendData(documentId);
        var hash = md5.GetHashAndReset(); // 16 bytes

        var result = Rc4.Transform(encryptionKey, hash);
        for (var i = 1; i <= 19; i++)
        {
            result = Rc4.Transform(XorKey(encryptionKey, i), result);
        }

        // Pad the 16-byte result out to 32 bytes (arbitrary padding per spec).
        var u = new byte[32];
        Array.Copy(result, 0, u, 0, 16);
        Array.Copy(PasswordPad, 0, u, 16, 16);
        return u;
    }

    // Algorithm 1: per-object encryption key.
    private byte[] GetObjectKey(int objectNumber, int generation)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(_encryptionKey);
        md5.AppendData(new[]
        {
            (byte)(objectNumber & 0xFF),
            (byte)((objectNumber >> 8) & 0xFF),
            (byte)((objectNumber >> 16) & 0xFF),
            (byte)(generation & 0xFF),
            (byte)((generation >> 8) & 0xFF)
        });

        var digest = md5.GetHashAndReset();
        var length = Math.Min(KeyLengthBytes + 5, 16);
        return digest.AsSpan(0, length).ToArray();
    }

    /// <summary>
    /// Encrypts every literal string (in non-stream objects) and the stream payload (in stream
    /// objects) of one indirect object, using its per-object key. RC4 preserves length, so the
    /// surrounding object structure and any <c>/Length</c> values remain valid.
    /// </summary>
    public byte[] EncryptObjectBody(int objectNumber, int generation, byte[] body)
    {
        var key = GetObjectKey(objectNumber, generation);

        var streamStart = IndexOf(body, StreamMarker, 0);
        if (streamStart >= 0)
        {
            var payloadStart = streamStart + StreamMarker.Length;
            var length = ParseStreamLength(body);
            if (length < 0 || payloadStart + length > body.Length)
            {
                return body; // Unexpected layout — leave untouched rather than corrupt.
            }

            var payload = new byte[length];
            Array.Copy(body, payloadStart, payload, 0, length);
            var encrypted = Rc4.Transform(key, payload);
            var result = (byte[])body.Clone();
            Array.Copy(encrypted, 0, result, payloadStart, length);
            return result;
        }

        return EncryptLiteralStrings(body, key);
    }

    private static byte[] EncryptLiteralStrings(byte[] body, byte[] key)
    {
        var output = new List<byte>(body.Length);
        var i = 0;
        while (i < body.Length)
        {
            if (body[i] != (byte)'(')
            {
                output.Add(body[i]);
                i++;
                continue;
            }

            // Capture the (escaped) content between balanced, unescaped parentheses.
            var content = new List<byte>();
            var depth = 1;
            var j = i + 1;
            while (j < body.Length && depth > 0)
            {
                var c = body[j];
                if (c == (byte)'\\' && j + 1 < body.Length)
                {
                    content.Add(c);
                    content.Add(body[j + 1]);
                    j += 2;
                    continue;
                }

                if (c == (byte)'(')
                {
                    depth++;
                }
                else if (c == (byte)')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        j++;
                        break;
                    }
                }

                content.Add(c);
                j++;
            }

            var raw = Unescape(content);
            var encrypted = Rc4.Transform(key, raw);
            output.Add((byte)'(');
            output.AddRange(Escape(encrypted));
            output.Add((byte)')');
            i = j;
        }

        return output.ToArray();
    }

    /// <summary>Builds the (unencrypted) <c>/Encrypt</c> dictionary object body.</summary>
    public byte[] BuildEncryptDictionary()
    {
        var sb = new StringBuilder();
        sb.Append("<< /Filter /Standard /V ").Append(Version)
          .Append(" /R ").Append(Revision)
          .Append(" /Length ").Append(KeyLengthBytes * 8)
          .Append(" /P ").Append(Permissions)
          .Append(" /O (");
        var bytes = new List<byte>(Encoding.ASCII.GetBytes(sb.ToString()));
        bytes.AddRange(Escape(OwnerEntry));
        bytes.AddRange(Encoding.ASCII.GetBytes(") /U ("));
        bytes.AddRange(Escape(UserEntry));
        bytes.AddRange(Encoding.ASCII.GetBytes(") >>\n"));
        return bytes.ToArray();
    }

    /// <summary>Generates a 16-byte document identifier for the trailer <c>/ID</c> and key derivation.</summary>
    public static byte[] GenerateDocumentId(PdfDocument document)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(Guid.NewGuid().ToByteArray());
        md5.AppendData(BitConverter.GetBytes(DateTime.UtcNow.Ticks));
        if (!string.IsNullOrEmpty(document.Info.Title))
        {
            md5.AppendData(Encoding.UTF8.GetBytes(document.Info.Title));
        }

        return md5.GetHashAndReset();
    }

    private static int ComputePermissionValue(PdfPermissions permissions)
    {
        // Start from all bits set, clear the two low reserved bits, clear the permission bits,
        // then set the granted ones. Bits 7-8 and 13-32 stay 1 (reserved, must be set).
        var p = unchecked((int)0xFFFFFFFF);
        p &= ~0b11;
        const int permissionMask = 4 | 8 | 16 | 32 | 256 | 512 | 1024 | 2048;
        p &= ~permissionMask;

        if (permissions.HasFlag(PdfPermissions.Print)) p |= 4;
        if (permissions.HasFlag(PdfPermissions.Modify)) p |= 8;
        if (permissions.HasFlag(PdfPermissions.Copy)) p |= 16;
        if (permissions.HasFlag(PdfPermissions.AnnotateAndFillForms)) p |= 32;
        if (permissions.HasFlag(PdfPermissions.FillForms)) p |= 256;
        if (permissions.HasFlag(PdfPermissions.ExtractForAccessibility)) p |= 512;
        if (permissions.HasFlag(PdfPermissions.Assemble)) p |= 1024;
        if (permissions.HasFlag(PdfPermissions.PrintHighResolution)) p |= 2048;

        return p;
    }

    private static byte[] PadPassword(string? password)
    {
        var passwordBytes = string.IsNullOrEmpty(password)
            ? Array.Empty<byte>()
            : Encoding.GetEncoding("ISO-8859-1").GetBytes(password);

        var padded = new byte[32];
        var take = Math.Min(passwordBytes.Length, 32);
        Array.Copy(passwordBytes, 0, padded, 0, take);
        Array.Copy(PasswordPad, 0, padded, take, 32 - take);
        return padded;
    }

    private static byte[] XorKey(byte[] key, int value)
    {
        var result = new byte[key.Length];
        for (var i = 0; i < key.Length; i++)
        {
            result[i] = (byte)(key[i] ^ value);
        }

        return result;
    }

    private static byte[] Unescape(List<byte> escaped)
    {
        var output = new List<byte>(escaped.Count);
        var i = 0;
        while (i < escaped.Count)
        {
            if (escaped[i] == (byte)'\\' && i + 1 < escaped.Count)
            {
                output.Add(escaped[i + 1]);
                i += 2;
                continue;
            }

            output.Add(escaped[i]);
            i++;
        }

        return output.ToArray();
    }

    private static IEnumerable<byte> Escape(byte[] data)
    {
        foreach (var b in data)
        {
            if (b == (byte)'\\' || b == (byte)'(' || b == (byte)')')
            {
                yield return (byte)'\\';
            }

            yield return b;
        }
    }

    private static readonly byte[] StreamMarker = Encoding.ASCII.GetBytes("stream\n");

    private static int ParseStreamLength(byte[] body)
    {
        // Match "/Length " (trailing space avoids "/Length1" used by embedded font streams).
        var marker = Encoding.ASCII.GetBytes("/Length ");
        var index = IndexOf(body, marker, 0);
        if (index < 0)
        {
            return -1;
        }

        var pos = index + marker.Length;
        var value = 0;
        var any = false;
        while (pos < body.Length && body[pos] >= (byte)'0' && body[pos] <= (byte)'9')
        {
            value = value * 10 + (body[pos] - (byte)'0');
            any = true;
            pos++;
        }

        return any ? value : -1;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }
}
