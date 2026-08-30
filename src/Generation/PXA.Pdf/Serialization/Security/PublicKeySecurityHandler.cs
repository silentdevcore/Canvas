using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PXA.Pdf.Serialization.Security;

/// <summary>Writes the interoperable Adobe.PubSec adbe.pkcs7.s5 AESV2 profile.</summary>
internal sealed class PublicKeySecurityHandler : IPdfSecurityHandler
{
    private const int SeedLength = 20;
    private readonly byte[] _fileKey;
    private readonly IReadOnlyList<byte[]> _recipientEnvelopes;
    private bool _disposed;

    private PublicKeySecurityHandler(
        byte[] fileKey,
        IReadOnlyList<byte[]> recipientEnvelopes,
        int permissions,
        bool encryptMetadata,
        byte[] documentId)
    {
        _fileKey = fileKey;
        _recipientEnvelopes = recipientEnvelopes;
        Permissions = permissions;
        EncryptMetadata = encryptMetadata;
        DocumentId = documentId;
    }

    public int Permissions { get; }
    public bool EncryptMetadata { get; }
    public byte[] DocumentId { get; }
    public string PdfVersion => "1.6";

    public static PublicKeySecurityHandler Create(
        PdfCertificateEncryptionOptions options,
        byte[] documentId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(documentId);
        if (options.Recipients.Count == 0)
        {
            throw new ArgumentException("Certificate encryption requires at least one recipient.", nameof(options));
        }

        var recipients = options.Recipients
            .Select(ValidateRecipient)
            .ToArray();
        var permissions = ComputePermissionValue(options.Permissions);
        var seed = RandomNumberGenerator.GetBytes(SeedLength);
        var payload = new byte[SeedLength + 4];
        seed.CopyTo(payload, 0);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(SeedLength), permissions);
        var envelopes = new List<byte[]>();
        byte[]? fileKey = null;
        try
        {
            var content = new ContentInfo(payload);
            var cms = new EnvelopedCms(
                content,
                new AlgorithmIdentifier(new Oid("2.16.840.1.101.3.4.1.2")));
            var cmsRecipients = new CmsRecipientCollection();
            foreach (var recipient in recipients)
            {
                cmsRecipients.Add(new CmsRecipient(
                    SubjectIdentifierType.IssuerAndSerialNumber,
                    recipient));
            }

            cms.Encrypt(cmsRecipients);
            envelopes.Add(cms.Encode());

            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            sha1.AppendData(seed);
            foreach (var envelope in envelopes) sha1.AppendData(envelope);
            if (!options.EncryptMetadata) sha1.AppendData([0xFF, 0xFF, 0xFF, 0xFF]);
            fileKey = sha1.GetHashAndReset().AsSpan(0, 16).ToArray();
            return new PublicKeySecurityHandler(
                fileKey, envelopes, permissions, options.EncryptMetadata, documentId);
        }
        catch
        {
            Zero(fileKey);
            foreach (var envelope in envelopes) Zero(envelope);
            throw;
        }
        finally
        {
            Zero(seed);
            Zero(payload);
        }
    }

    public byte[] EncryptObjectBody(int objectNumber, int generation, byte[] body)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Aes128SecurityHandler.EncryptObjectBody(
            _fileKey, EncryptMetadata, objectNumber, generation, body);
    }

    public byte[] BuildEncryptDictionary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var recipientArray = string.Join(
            " ",
            _recipientEnvelopes.Select(static value => $"<{Convert.ToHexString(value)}>"));
        return Encoding.ASCII.GetBytes(
            $"<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s5 /V 4 /Length 128 /P {Permissions} " +
            "/CF << /DefaultCryptFilter << /CFM /AESV2 /AuthEvent /DocOpen /Length 128 " +
            $"/Recipients [{recipientArray}] /EncryptMetadata {(EncryptMetadata ? "true" : "false")} >> >> " +
            "/StmF /DefaultCryptFilter /StrF /DefaultCryptFilter >>\n");
    }

    private static X509Certificate2 ValidateRecipient(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is null || rsa.KeySize < 2048)
        {
            throw new CryptographicException(
                "PDF certificate encryption requires an RSA recipient certificate with at least 2048 bits.");
        }

        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        if (keyUsage is not null &&
            !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment) &&
            !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyAgreement))
        {
            throw new CryptographicException(
                "The recipient certificate is not valid for key encipherment or key agreement.");
        }

        return certificate;
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

    private static void Zero(byte[]? value)
    {
        if (value is not null) CryptographicOperations.ZeroMemory(value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Zero(_fileKey);
        foreach (var envelope in _recipientEnvelopes) Zero(envelope);
        _disposed = true;
    }
}
