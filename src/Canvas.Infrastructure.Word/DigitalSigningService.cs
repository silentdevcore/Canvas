using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Canvas.Infrastructure.Word;

/// <summary>
/// Signs a DOCX package with an X.509 certificate using an OPC package digital signature
/// (ECMA-376 Part 2 / ISO 29500, the same shape Word produces). The caller supplies a
/// PFX/P12 blob; the returned byte array is the signed DOCX.
/// </summary>
/// <remarks>
/// The signature is structurally an OPC package signature: a <c>&lt;Manifest&gt;</c> inside the
/// package <c>&lt;Object&gt;</c> lists every signed part with its <c>?ContentType=</c> URI; XML
/// content parts are digested as raw octets; relationship (<c>.rels</c>) parts go through the OPC
/// <c>RelationshipTransform</c> + C14N. All digests and the <c>SignedInfo</c> signature are computed
/// over true Canonical XML, so the signature is internally consistent and verifiable. Final
/// acceptance by Microsoft Word should still be confirmed against a real Office install.
/// </remarks>
public static class DigitalSigningService
{
    private const string RDigSig    = "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/origin";
    private const string RDigSigSig = "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/signature";
    private const string CTypeSig   = "application/vnd.openxmlformats-package.digital-signature-xmlsignature+xml";
    private const string CTypeOrig  = "application/vnd.openxmlformats-package.digital-signature-origin";

    private const string XmlDsig    = "http://www.w3.org/2000/09/xmldsig#";
    private const string MdssiNs    = "http://schemas.openxmlformats.org/package/2006/digital-signature";
    private const string PkgRelNs   = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string C14n       = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
    private const string RelTransform   = "http://schemas.openxmlformats.org/package/2006/RelationshipTransform";
    private const string Sha256Method   = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string RsaSha256Method = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string ObjectRefType   = "http://www.w3.org/2000/09/xmldsig#Object";

    private const string SigId = "idPackageSignature";
    private const string ObjId = "idPackageObject";

    // ── Public API ────────────────────────────────────────────────────────────

    public static byte[] SignDocx(Stream docxStream, byte[] pfxBytes, string? pfxPassword = null)
    {
        // EphemeralKeySet keeps the private key out of the disk-backed key store, but it is
        // unsupported on macOS/Linux — fall back to the default (temporary on-disk) key set there.
        var storageFlags = OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet
            : X509KeyStorageFlags.Exportable;

        using var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, pfxPassword, storageFlags);

        if (!cert.HasPrivateKey)
            throw new InvalidOperationException("The certificate does not contain a private key.");

        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Only RSA private keys are supported.");

        // Read source DOCX into an in-memory part map.
        using var srcMs = new MemoryStream();
        docxStream.CopyTo(srcMs);
        var parts = ReadParts(srcMs.ToArray());

        // Apply the signature wiring to the package, then sign the *final* parts.
        parts["[Content_Types].xml"] = Encoding.UTF8.GetBytes(PatchContentTypes(GetText(parts, "[Content_Types].xml")));
        parts["_rels/.rels"]         = Encoding.UTF8.GetBytes(PatchRootRels(GetText(parts, "_rels/.rels")));
        parts["_xmlsignatures/origin.sigs"]                = [];
        parts["_xmlsignatures/_rels/origin.sigs.rels"]     = Encoding.UTF8.GetBytes(BuildOriginRelsXml());

        var signTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var certB64  = Convert.ToBase64String(cert.RawData);
        var sigXml   = BuildSignatureXml(parts, signTime, certB64, rsa);
        parts["_xmlsignatures/sig1.xml"] = Encoding.UTF8.GetBytes(sigXml);

        // Emit the signed package.
        using var outMs = new MemoryStream();
        using (var zipOut = new ZipArchive(outMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in parts)
            {
                var entry = zipOut.CreateEntry(name, CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(bytes, 0, bytes.Length);
            }
        }
        return outMs.ToArray();
    }

    // ── Package helpers ────────────────────────────────────────────────────────

    private static Dictionary<string, byte[]> ReadParts(byte[] docxBytes)
    {
        var parts = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var zip = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue; // directory marker
            using var s  = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            parts[entry.FullName] = ms.ToArray();
        }
        return parts;
    }

    private static string GetText(Dictionary<string, byte[]> parts, string name)
        => parts.TryGetValue(name, out var b) ? DecodeText(b) : string.Empty;

    /// <summary>Decodes part bytes as UTF-8, stripping a leading BOM so XmlDocument.LoadXml accepts it.</summary>
    private static string DecodeText(byte[] bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
            : Encoding.UTF8.GetString(bytes);

    // ── Signature document ─────────────────────────────────────────────────────

    private static string BuildSignatureXml(
        Dictionary<string, byte[]> parts, string signTime, string certB64, RSA rsa)
    {
        // Build as DOM nodes so the bytes we digest/sign are the canonical form a verifier
        // recomputes — not a pretty-printed string.
        var doc = new XmlDocument { PreserveWhitespace = true };

        var signature = doc.CreateElement("Signature", XmlDsig);
        signature.SetAttribute("Id", SigId);
        doc.AppendChild(signature);

        // The package <Object> carries the Manifest (per-part digests) + the signing time.
        var packageObject = BuildPackageObject(doc, parts, signTime);
        var objDigest     = Convert.ToBase64String(Sha256OfCanonical(packageObject));

        // SignedInfo references only the package object; everything signed lives under it.
        var signedInfo = doc.CreateElement("SignedInfo", XmlDsig);
        signedInfo.AppendChild(AlgorithmElement(doc, "CanonicalizationMethod", C14n));
        signedInfo.AppendChild(AlgorithmElement(doc, "SignatureMethod", RsaSha256Method));
        var objRef = ReferenceElement(doc, "#" + ObjId, transforms: [C14n], objDigest);
        objRef.SetAttribute("Type", ObjectRefType);
        signedInfo.AppendChild(objRef);
        signature.AppendChild(signedInfo);

        // SignatureValue over canonicalized SignedInfo.
        var sigBytes = rsa.SignData(Canonicalize(signedInfo), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sigValue = doc.CreateElement("SignatureValue", XmlDsig);
        sigValue.InnerText = Convert.ToBase64String(sigBytes);
        signature.AppendChild(sigValue);

        // KeyInfo.
        var keyInfo  = doc.CreateElement("KeyInfo", XmlDsig);
        var x509Data = doc.CreateElement("X509Data", XmlDsig);
        var x509Cert = doc.CreateElement("X509Certificate", XmlDsig);
        x509Cert.InnerText = certB64;
        x509Data.AppendChild(x509Cert);
        keyInfo.AppendChild(x509Data);
        signature.AppendChild(keyInfo);

        signature.AppendChild(packageObject);

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + doc.OuterXml;
    }

    private static XmlElement BuildPackageObject(XmlDocument doc, Dictionary<string, byte[]> parts, string signTime)
    {
        var obj = doc.CreateElement("Object", XmlDsig);
        obj.SetAttribute("Id", ObjId);

        // ── Manifest: one Reference per signed part ──────────────────────────────
        var manifest = doc.CreateElement("Manifest", XmlDsig);
        var resolver = new ContentTypeResolver(GetText(parts, "[Content_Types].xml"));

        var references = new List<(string Uri, XmlElement Reference)>();
        foreach (var (name, bytes) in parts)
        {
            if (name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) continue; // not an addressable part
            if (name.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase)) continue;   // signature infra is not signed

            var partName    = "/" + name.Replace('\\', '/');
            var contentType = resolver.Resolve(partName);
            if (contentType is null) continue;

            var uri = partName + "?ContentType=" + contentType;

            if (name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            {
                var ids = ReadSignableRelationshipIds(DecodeText(bytes));
                if (ids.Count == 0) continue; // nothing left to sign (e.g. only the signature-origin rel)

                var (transforms, digest) = BuildRelationshipReference(doc, DecodeText(bytes), ids);
                var reference = doc.CreateElement("Reference", XmlDsig);
                reference.SetAttribute("URI", uri);
                reference.AppendChild(transforms);
                reference.AppendChild(AlgorithmElement(doc, "DigestMethod", Sha256Method));
                var dv = doc.CreateElement("DigestValue", XmlDsig);
                dv.InnerText = Convert.ToBase64String(digest);
                reference.AppendChild(dv);
                references.Add((uri, reference));
            }
            else
            {
                // Content parts are digested as raw octets (identity transform).
                var digest = Convert.ToBase64String(SHA256.HashData(bytes));
                references.Add((uri, ReferenceElement(doc, uri, transforms: null, digest)));
            }
        }

        foreach (var (_, reference) in references.OrderBy(r => r.Uri, StringComparer.Ordinal))
            manifest.AppendChild(reference);
        obj.AppendChild(manifest);

        // ── Signing time ─────────────────────────────────────────────────────────
        var props = doc.CreateElement("SignatureProperties", XmlDsig);
        var prop  = doc.CreateElement("SignatureProperty", XmlDsig);
        prop.SetAttribute("Id", "idSignatureTime");
        prop.SetAttribute("Target", "#" + SigId);

        var sigTime = doc.CreateElement("mdssi", "SignatureTime", MdssiNs);
        var format  = doc.CreateElement("mdssi", "Format", MdssiNs);
        format.InnerText = "YYYY-MM-DDThh:mm:ssTZD";
        var value   = doc.CreateElement("mdssi", "Value", MdssiNs);
        value.InnerText = signTime;
        sigTime.AppendChild(format);
        sigTime.AppendChild(value);
        prop.AppendChild(sigTime);
        props.AppendChild(prop);
        obj.AppendChild(props);

        return obj;
    }

    // ── Relationship transform ─────────────────────────────────────────────────

    /// <summary>Relationship Ids to sign — every relationship except signature infrastructure.</summary>
    private static List<string> ReadSignableRelationshipIds(string relsXml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(relsXml);
        var ids = new List<string>();
        foreach (XmlNode n in doc.DocumentElement!.ChildNodes)
        {
            if (n is not XmlElement e || e.LocalName != "Relationship") continue;
            var type = e.GetAttribute("Type");
            if (type is RDigSig or RDigSigSig) continue; // exclude the signature-origin wiring
            ids.Add(e.GetAttribute("Id"));
        }
        return ids;
    }

    private static (XmlElement Transforms, byte[] Digest) BuildRelationshipReference(
        XmlDocument doc, string relsXml, List<string> includeIds)
    {
        var transforms = doc.CreateElement("Transforms", XmlDsig);
        var relTransform = AlgorithmElement(doc, "Transform", RelTransform);
        foreach (var id in includeIds.OrderBy(i => i, StringComparer.Ordinal))
        {
            var rr = doc.CreateElement("mdssi", "RelationshipReference", MdssiNs);
            rr.SetAttribute("SourceId", id);
            relTransform.AppendChild(rr);
        }
        transforms.AppendChild(relTransform);
        transforms.AppendChild(AlgorithmElement(doc, "Transform", C14n));

        var digest = SHA256.HashData(ApplyRelationshipTransform(relsXml, includeIds));
        return (transforms, digest);
    }

    /// <summary>
    /// OPC RelationshipTransform: keep only the selected relationships, default any missing
    /// TargetMode to "Internal", sort by Id (ordinal), then canonicalize (C14N).
    /// </summary>
    private static byte[] ApplyRelationshipTransform(string relsXml, ICollection<string> includeIds)
    {
        var source = new XmlDocument { PreserveWhitespace = true };
        source.LoadXml(relsXml);

        var selected = new List<XmlElement>();
        foreach (XmlNode n in source.DocumentElement!.ChildNodes)
            if (n is XmlElement e && e.LocalName == "Relationship" && includeIds.Contains(e.GetAttribute("Id")))
                selected.Add(e);

        var outDoc = new XmlDocument();
        var root   = outDoc.CreateElement("Relationships", PkgRelNs);
        outDoc.AppendChild(root);
        foreach (var e in selected.OrderBy(x => x.GetAttribute("Id"), StringComparer.Ordinal))
        {
            var imported = (XmlElement)outDoc.ImportNode(e, deep: true);
            if (!imported.HasAttribute("TargetMode"))
                imported.SetAttribute("TargetMode", "Internal");
            root.AppendChild(imported);
        }
        return Canonicalize(root);
    }

    // ── XML / C14N helpers ─────────────────────────────────────────────────────

    private static XmlElement AlgorithmElement(XmlDocument doc, string name, string algorithm)
    {
        var el = doc.CreateElement(name, XmlDsig);
        el.SetAttribute("Algorithm", algorithm);
        return el;
    }

    private static XmlElement ReferenceElement(XmlDocument doc, string uri, string[]? transforms, string digestB64)
    {
        var reference = doc.CreateElement("Reference", XmlDsig);
        reference.SetAttribute("URI", uri);

        if (transforms is { Length: > 0 })
        {
            var transformsEl = doc.CreateElement("Transforms", XmlDsig);
            foreach (var t in transforms)
                transformsEl.AppendChild(AlgorithmElement(doc, "Transform", t));
            reference.AppendChild(transformsEl);
        }

        reference.AppendChild(AlgorithmElement(doc, "DigestMethod", Sha256Method));
        var digest = doc.CreateElement("DigestValue", XmlDsig);
        digest.InnerText = digestB64;
        reference.AppendChild(digest);
        return reference;
    }

    private static byte[] Sha256OfCanonical(XmlElement element) => SHA256.HashData(Canonicalize(element));

    private static byte[] Canonicalize(XmlElement element)
    {
        // Re-parse the element in isolation so its namespace context is self-contained,
        // matching how a verifier canonicalizes the same node-set.
        var isolated = new XmlDocument { PreserveWhitespace = true };
        isolated.LoadXml(element.OuterXml);

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(isolated);
        using var output = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        output.CopyTo(ms);
        return ms.ToArray();
    }

    private static string BuildOriginRelsXml() =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="{PkgRelNs}">
          <Relationship Id="rSig1" Type="{RDigSigSig}" Target="sig1.xml"/>
        </Relationships>
        """;

    // ── Content-Types & root rels patching ────────────────────────────────────

    private static string PatchContentTypes(string contentTypesXml)
    {
        if (string.IsNullOrWhiteSpace(contentTypesXml)) return BuildMinimalContentTypes();

        var doc = new XmlDocument();
        doc.LoadXml(contentTypesXml);
        var ns   = "http://schemas.openxmlformats.org/package/2006/content-types";
        var root = doc.DocumentElement!;

        bool hasOrigin = false, hasSig = false;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is not XmlElement e) continue;
            if (e.GetAttribute("ContentType") == CTypeOrig) hasOrigin = true;
            if (e.GetAttribute("ContentType") == CTypeSig)  hasSig    = true;
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

        return ToXmlString(doc);
    }

    private static string PatchRootRels(string rootRelsXml)
    {
        if (string.IsNullOrWhiteSpace(rootRelsXml)) return BuildMinimalRootRels();

        var doc = new XmlDocument();
        doc.LoadXml(rootRelsXml);
        var root = doc.DocumentElement!;

        foreach (XmlNode node in root.ChildNodes)
            if (node is XmlElement e && e.GetAttribute("Type") == RDigSig)
                return ToXmlString(doc); // already wired

        var rel = doc.CreateElement("Relationship", PkgRelNs);
        rel.SetAttribute("Id", "rDigSigOrigin");
        rel.SetAttribute("Type", RDigSig);
        rel.SetAttribute("Target", "/_xmlsignatures/origin.sigs");
        root.AppendChild(rel);

        return ToXmlString(doc);
    }

    private static string ToXmlString(XmlDocument doc)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false };
        using (var w = XmlWriter.Create(ms, settings))
            doc.Save(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string BuildMinimalContentTypes() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"></Types>""";

    private static string BuildMinimalRootRels() =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{PkgRelNs}"></Relationships>""";

    // ── Content-type resolution ────────────────────────────────────────────────

    private sealed class ContentTypeResolver
    {
        private readonly Dictionary<string, string> _defaults  = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);

        public ContentTypeResolver(string contentTypesXml)
        {
            if (string.IsNullOrWhiteSpace(contentTypesXml)) return;
            var doc = new XmlDocument();
            doc.LoadXml(contentTypesXml);
            foreach (XmlNode n in doc.DocumentElement!.ChildNodes)
            {
                if (n is not XmlElement e) continue;
                if (e.LocalName == "Default")
                    _defaults[e.GetAttribute("Extension")] = e.GetAttribute("ContentType");
                else if (e.LocalName == "Override")
                    _overrides[e.GetAttribute("PartName")] = e.GetAttribute("ContentType");
            }
        }

        public string? Resolve(string partName)
        {
            if (_overrides.TryGetValue(partName, out var ct)) return ct;
            var ext = Path.GetExtension(partName).TrimStart('.');
            return _defaults.TryGetValue(ext, out var d) ? d : null;
        }
    }
}
