using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PVAD.Importer.Objects;

namespace PVAD.Importer.Security;

/// <summary>Opens Adobe.PubSec adbe.pkcs7.s5 documents using an X.509 private key.</summary>
internal sealed class PdfPublicKeySecurityHandler : IPdfDocumentSecurityHandler
{
    private readonly byte[] _fileKey;
    private readonly bool _encryptMetadata;
    private bool _disposed;

    private PdfPublicKeySecurityHandler(byte[] fileKey, bool encryptMetadata)
    {
        _fileKey = fileKey;
        _encryptMetadata = encryptMetadata;
    }

    public bool ShouldDecryptString => true;

    public static bool IsSupported(PdfDictionary? encrypt) =>
        encrypt?["Filter"] is PdfName { Value: "Adobe.PubSec" } &&
        encrypt["SubFilter"] is PdfName { Value: "adbe.pkcs7.s5" } &&
        ReadInteger(encrypt["V"]) == 4;

    public static PdfPublicKeySecurityHandler Open(
        PdfDictionary encrypt,
        X509Certificate2 certificate,
        out PdfDecryptionResult result)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        ArgumentNullException.ThrowIfNull(certificate);
        if (!certificate.HasPrivateKey)
        {
            throw new CryptographicException(
                "Opening a certificate-encrypted PDF requires the matching private key.");
        }

        if (!IsSupported(encrypt))
        {
            throw new NotSupportedException(
                "Only Adobe.PubSec /adbe.pkcs7.s5 with the AESV2 crypt filter is supported.");
        }

        var filter = ReadSelectedCryptFilter(encrypt);
        if (filter["CFM"] is not PdfName { Value: "AESV2" })
        {
            throw new NotSupportedException("The public-key crypt filter must use /AESV2.");
        }

        var encryptMetadata = filter["EncryptMetadata"] is not PdfBoolean { Value: false };
        var envelopes = ReadRecipients(filter);
        byte[]? seed = null;
        var permissions = 0;
        try
        {
            foreach (var envelope in envelopes)
            {
                try
                {
                    var cms = new EnvelopedCms();
                    cms.Decode(envelope);
                    cms.Decrypt(new X509Certificate2Collection(certificate));
                    var payload = cms.ContentInfo.Content;
                    if (payload.Length != 24)
                    {
                        throw new InvalidDataException(
                            "A document-level public-key recipient payload must contain 20 seed bytes and 4 permission bytes.");
                    }

                    seed = payload.AsSpan(0, 20).ToArray();
                    permissions = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(20, 4));
                    break;
                }
                catch (CryptographicException)
                {
                    // Try the next recipient envelope without disclosing recipient identities.
                }
            }

            if (seed is null)
            {
                throw new PdfPasswordException(
                    "The supplied certificate does not open this document.");
            }

            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            sha1.AppendData(seed);
            foreach (var envelope in envelopes) sha1.AppendData(envelope);
            if (!encryptMetadata) sha1.AppendData([0xFF, 0xFF, 0xFF, 0xFF]);
            var fileKey = sha1.GetHashAndReset().AsSpan(0, 16).ToArray();
            result = new PdfDecryptionResult(
                PdfPasswordKind.Certificate,
                (PdfDocumentPermissions)(permissions & (int)PdfDocumentPermissions.All),
                encryptMetadata,
                PermissionsVerified: true);
            return new PdfPublicKeySecurityHandler(fileKey, encryptMetadata);
        }
        finally
        {
            Zero(seed);
            foreach (var envelope in envelopes) Zero(envelope);
        }
    }

    public bool ShouldDecryptStream(PdfDictionary dictionary)
    {
        if (dictionary["Type"] is PdfName { Value: "XRef" }) return false;
        if (!_encryptMetadata && dictionary["Type"] is PdfName { Value: "Metadata" }) return false;
        if (ContainsExplicitCryptFilter(dictionary))
        {
            throw new NotSupportedException(
                "Explicit stream /Crypt filters are not supported for public-key encrypted documents.");
        }

        return true;
    }

    public byte[] DecryptValue(ReadOnlySpan<byte> ciphertext, int objectNumber, int generation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ciphertext.Length < 32 || (ciphertext.Length - 16) % 16 != 0)
        {
            throw new PdfPasswordException("An encrypted value is not a well-formed AES-128 block.");
        }

        var key = GetObjectKey(objectNumber, generation);
        try
        {
            using var aes = Aes.Create();
            aes.Key = key;
            return aes.DecryptCbc(ciphertext[16..], ciphertext[..16], PaddingMode.PKCS7);
        }
        finally
        {
            Zero(key);
        }
    }

    private byte[] GetObjectKey(int objectNumber, int generation)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(_fileKey);
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

    private static PdfDictionary ReadSelectedCryptFilter(PdfDictionary encrypt)
    {
        if (encrypt["StrF"] is not PdfName stringFilter ||
            encrypt["StmF"] is not PdfName streamFilter ||
            stringFilter.Value != streamFilter.Value ||
            encrypt["CF"] is not PdfDictionary filters ||
            filters[stringFilter.Value] is not PdfDictionary filter)
        {
            throw new NotSupportedException(
                "Public-key string and stream selectors must reference one document-level crypt filter.");
        }

        return filter;
    }

    private static List<byte[]> ReadRecipients(PdfDictionary filter)
    {
        var recipients = filter["Recipients"] switch
        {
            PdfString recipient => [recipient.GetDecodedBytes().ToArray()],
            PdfArray array => array.Items.Select(item => item is PdfString value
                    ? value.GetDecodedBytes().ToArray()
                    : throw new InvalidDataException("A public-key recipient entry must be a byte string."))
                .ToList(),
            _ => throw new InvalidDataException("The public-key crypt filter has no recipient envelopes.")
        };
        if (recipients.Count == 0)
        {
            throw new InvalidDataException("The public-key crypt filter has no recipient envelopes.");
        }

        return recipients;
    }

    private static int ReadInteger(PdfObject? value) => value switch
    {
        PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
        PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
        _ => 0
    };

    private static bool ContainsExplicitCryptFilter(PdfDictionary dictionary) =>
        dictionary["Filter"] is PdfName { Value: "Crypt" } ||
        dictionary["Filter"] is PdfArray filters &&
        filters.Items.OfType<PdfName>().Any(static filter => filter.Value == "Crypt");

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
