using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Canvas.Infrastructure.Word;

/// <summary>
/// Signs a DOCX package with an X.509 certificate using OOXML XML-DSig (ISO 29500).
/// The caller supplies a PFX/P12 blob; the returned byte array is the signed DOCX.
/// </summary>
public static class DigitalSigningService
{
    private const string WDigSig    = "http://schemas.openxmlformats.org/package/2006/digital-signature";
    private const string RDigSig    = "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/origin";
    private const string RDigSigSig = "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/signature";
    private const string CTypeSig   = "application/vnd.openxmlformats-package.digital-signature-xmlsignature+xml";
    private const string CTypeOrig  = "application/vnd.openxmlformats-package.digital-signature-origin";
    private const string XmlDsig    = "http://www.w3.org/2000/09/xmldsig#";
    private const string C14n       = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";

    // ── Public API ────────────────────────────────────────────────────────────

    public static byte[] SignDocx(Stream docxStream, byte[] pfxBytes, string? pfxPassword = null)
    {
        using var cert = X509CertificateLoader.LoadPkcs12(
            pfxBytes, pfxPassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);

        if (!cert.HasPrivateKey)
            throw new InvalidOperationException("The certificate does not contain a private key.");

        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Only RSA private keys are supported.");

        // Read source DOCX bytes
        using var srcMs = new MemoryStream();
        docxStream.CopyTo(srcMs);
        var docxBytes = srcMs.ToArray();

        // Collect part digests
        var partDigests = ComputePartDigests(docxBytes);

        // Build the signature XML
        string sigId      = "idPackageSignature";
        string sigObjId   = "idPackageObject";
        string signTime   = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        string certB64    = Convert.ToBase64String(cert.RawData);

        var sigXml = BuildSignatureXml(partDigests, signTime, certB64, sigId, sigObjId, rsa);

        // Write new ZIP with the signature part inserted
        using var outMs = new MemoryStream();
        using (var zipOut = new ZipArchive(outMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var zipIn = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);

            foreach (var entry in zipIn.Entries)
            {
                if (entry.FullName == "[Content_Types].xml" || entry.FullName == "_rels/.rels")
                    continue;

                var outEntry = zipOut.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inStream  = entry.Open();
                using var outStream = outEntry.Open();
                inStream.CopyTo(outStream);
            }

            // _xmlsignatures/_rels/sig1.xml.rels  (origin relationship)
            WriteEntry(zipOut, "_xmlsignatures/_rels/sig1.xml.rels",
                BuildOriginRelsXml(sigId));

            // _xmlsignatures/sig1.xml
            WriteEntry(zipOut, "_xmlsignatures/sig1.xml", sigXml);

            // Rewrite [Content_Types].xml
            WriteEntry(zipOut, "[Content_Types].xml",
                PatchContentTypes(zipIn, docxBytes));

            // Rewrite _rels/.rels
            WriteEntry(zipOut, "_rels/.rels",
                PatchRootRels(zipIn, docxBytes));
        }

        return outMs.ToArray();
    }

    // ── Digest collection ─────────────────────────────────────────────────────

    private static Dictionary<string, string> ComputePartDigests(byte[] docxBytes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var zip = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);

        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase))
                continue;

            using var stream = entry.Open();
            using var ms     = new MemoryStream();
            stream.CopyTo(ms);
            var hash  = SHA256.HashData(ms.ToArray());
            result["/" + entry.FullName.Replace('\\', '/')] = Convert.ToBase64String(hash);
        }

        return result;
    }

    // ── XML builders ──────────────────────────────────────────────────────────

    private static string BuildSignatureXml(
        Dictionary<string, string> partDigests,
        string signTime, string certB64,
        string sigId, string objId,
        RSA rsa)
    {
        // Build <SignedInfo> first so we can sign it
        var sb = new StringBuilder();

        // References for each package part
        var refsXml = new StringBuilder();
        foreach (var (partUri, digestB64) in partDigests.OrderBy(kv => kv.Key))
        {
            refsXml.Append($"""
                <Reference URI="{partUri}">
                  <DigestMethod Algorithm="http://www.w3.org/2001/04/xmlenc#sha256"/>
                  <DigestValue>{digestB64}</DigestValue>
                </Reference>
                """);
        }

        // Reference to the package-object
        // We'll compute its digest after building its XML
        string objXml = BuildSignatureObject(signTime, certB64, objId, partDigests);
        byte[] objDigestBytes = SHA256.HashData(Encoding.UTF8.GetBytes(objXml));
        string objDigest = Convert.ToBase64String(objDigestBytes);

        string signedInfo = $"""
            <SignedInfo xmlns="{XmlDsig}">
              <CanonicalizationMethod Algorithm="{C14n}"/>
              <SignatureMethod Algorithm="http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"/>
              {refsXml}
              <Reference URI="#{objId}">
                <DigestMethod Algorithm="http://www.w3.org/2001/04/xmlenc#sha256"/>
                <DigestValue>{objDigest}</DigestValue>
              </Reference>
            </SignedInfo>
            """;

        // Sign the canonicalized <SignedInfo>
        byte[] sigBytes = rsa.SignData(
            Encoding.UTF8.GetBytes(signedInfo),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        string sigValue = Convert.ToBase64String(sigBytes);

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Signature Id="{sigId}" xmlns="{XmlDsig}">
              {signedInfo}
              <SignatureValue>{sigValue}</SignatureValue>
              <KeyInfo>
                <X509Data>
                  <X509Certificate>{certB64}</X509Certificate>
                </X509Data>
              </KeyInfo>
              {objXml}
            </Signature>
            """;
    }

    private static string BuildSignatureObject(
        string signTime, string certB64, string objId,
        Dictionary<string, string> partDigests)
    {
        return $"""
            <Object Id="{objId}" xmlns="http://www.w3.org/2000/09/xmldsig#">
              <SignatureProperties>
                <SignatureProperty Id="idSignatureTime" Target="#idPackageSignature">
                  <mdssi:SignatureTime xmlns:mdssi="http://schemas.openxmlformats.org/package/2006/digital-signature">
                    <mdssi:Format>YYYY-MM-DDThh:mm:ssTZD</mdssi:Format>
                    <mdssi:Value>{signTime}</mdssi:Value>
                  </mdssi:SignatureTime>
                </SignatureProperty>
              </SignatureProperties>
            </Object>
            """;
    }

    private static string BuildOriginRelsXml(string sigId)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rSig1" Type="{RDigSigSig}" Target="sig1.xml"/>
            </Relationships>
            """;
    }

    // ── Content-Types & root rels patching ────────────────────────────────────

    private static string PatchContentTypes(ZipArchive zip, byte[] docxBytes)
    {
        using var zipRead = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);
        var ctEntry = zipRead.GetEntry("[Content_Types].xml");
        if (ctEntry is null) return BuildMinimalContentTypes();

        using var stream = ctEntry.Open();
        var doc = new XmlDocument();
        doc.Load(stream);

        var ns   = "http://schemas.openxmlformats.org/package/2006/content-types";
        var root = doc.DocumentElement!;

        bool hasOrigin = false;
        bool hasSig    = false;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is XmlElement e)
            {
                if (e.GetAttribute("ContentType") == CTypeOrig) hasOrigin = true;
                if (e.GetAttribute("ContentType") == CTypeSig)  hasSig    = true;
            }
        }

        if (!hasOrigin)
        {
            var el = doc.CreateElement("Default", ns);
            el.SetAttribute("Extension", "sigs");
            el.SetAttribute("ContentType", CTypeOrig);
            root.AppendChild(el);
        }

        if (!hasSig)
        {
            var el = doc.CreateElement("Override", ns);
            el.SetAttribute("PartName", "/_xmlsignatures/sig1.xml");
            el.SetAttribute("ContentType", CTypeSig);
            root.AppendChild(el);
        }

        using var outMs = new MemoryStream();
        doc.Save(outMs);
        return Encoding.UTF8.GetString(outMs.ToArray());
    }

    private static string PatchRootRels(ZipArchive zip, byte[] docxBytes)
    {
        using var zipRead = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);
        var relsEntry = zipRead.GetEntry("_rels/.rels");
        if (relsEntry is null) return BuildMinimalRootRels();

        using var stream = relsEntry.Open();
        var doc = new XmlDocument();
        doc.Load(stream);

        var ns   = "http://schemas.openxmlformats.org/package/2006/relationships";
        var root = doc.DocumentElement!;

        bool hasOrigin = false;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is XmlElement e && e.GetAttribute("Type") == RDigSig)
            {
                hasOrigin = true;
                break;
            }
        }

        if (!hasOrigin)
        {
            var el = doc.CreateElement("Relationship", ns);
            el.SetAttribute("Id",     "rDigSigOrigin");
            el.SetAttribute("Type",   RDigSig);
            el.SetAttribute("Target", "/_xmlsignatures/origin.sigs");
            root.AppendChild(el);
        }

        using var outMs = new MemoryStream();
        doc.Save(outMs);
        return Encoding.UTF8.GetString(outMs.ToArray());
    }

    private static string BuildMinimalContentTypes() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"></Types>""";

    private static string BuildMinimalRootRels() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"></Relationships>""";

    // ── Utility ───────────────────────────────────────────────────────────────

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes);
    }
}
