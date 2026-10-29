using System.Text;
using System.Xml;
using System.Xml.Linq;
using PVAD.Pdf;

namespace PVAD.Importer.EInvoicing;

/// <summary>
/// Rewrites a document XMP packet so that it identifies PDF/A-3b and declares the extension
/// schema of an embedded invoice. Every other description - Dublin Core, the PDF schema, the
/// basic XMP schema and any vendor schema - is carried over untouched, because attaching an
/// invoice must not silently drop metadata the source document already published.
/// </summary>
internal static class PdfEInvoiceXmpWriter
{
    internal const int MaximumXmpBytes = 4 * 1024 * 1024;

    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Meta = "adobe:ns:meta/";
    private static readonly XNamespace PdfAId = "http://www.aiim.org/pdfa/ns/id/";
    private static readonly XNamespace PdfAExtension = "http://www.aiim.org/pdfa/ns/extension/";
    private static readonly XNamespace PdfASchema = "http://www.aiim.org/pdfa/ns/schema#";
    private static readonly XNamespace PdfAProperty = "http://www.aiim.org/pdfa/ns/property#";

    /// <summary>
    /// Produces the XMP packet for the updated document. <paramref name="existingXmp"/> may be
    /// empty or unreadable; a minimal packet is then created instead of failing the attachment.
    /// </summary>
    internal static byte[] Build(
        ReadOnlyMemory<byte> existingXmp,
        PdfAssociatedFileXmpMetadata? metadata,
        out bool existingPacketPreserved)
    {
        var document = TryLoad(existingXmp);
        existingPacketPreserved = document is not null;
        var rdf = document?.Descendants(Rdf + "RDF").FirstOrDefault();
        if (rdf is null)
        {
            existingPacketPreserved = false;
            rdf = new XElement(Rdf + "RDF");
            document = new XDocument(new XElement(Meta + "xmpmeta", rdf));
        }

        RemovePdfAIdentification(rdf);
        if (metadata is not null)
        {
            RemoveExtensionSchema(rdf, metadata.NamespaceUri);
            RemovePropertyValues(rdf, metadata.NamespaceUri);
        }

        rdf.Add(new XElement(
            Rdf + "Description",
            new XAttribute(Rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + "pdfaid", PdfAId.NamespaceName),
            new XElement(PdfAId + "part", "3"),
            new XElement(PdfAId + "conformance", "B")));

        if (metadata is not null)
        {
            rdf.Add(BuildExtensionSchema(metadata));
            rdf.Add(BuildPropertyValues(metadata));
        }

        return Serialize(document!);
    }

    private static XDocument? TryLoad(ReadOnlyMemory<byte> existingXmp)
    {
        if (existingXmp.IsEmpty || existingXmp.Length > MaximumXmpBytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(existingXmp.ToArray(), writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumXmpBytes,
                MaxCharactersFromEntities = 0,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// XMP allows simple properties as elements or as attributes on rdf:Description. Both
    /// spellings have to disappear, otherwise a stale pdfaid:part would survive next to the new one.
    /// </summary>
    private static void RemovePdfAIdentification(XElement rdf)
    {
        rdf.Descendants()
            .Where(static element => element.Name.Namespace == PdfAId)
            .ToArray()
            .Remove();
        foreach (var description in rdf.Elements(Rdf + "Description").ToArray())
        {
            description.Attributes()
                .Where(static attribute => attribute.Name.Namespace == PdfAId)
                .ToArray()
                .Remove();
            RemoveIfEmpty(description);
        }
    }

    private static void RemoveExtensionSchema(XElement rdf, string namespaceUri)
    {
        foreach (var schemas in rdf.Descendants(PdfAExtension + "schemas").ToArray())
        {
            foreach (var item in schemas.Descendants(Rdf + "li").ToArray())
            {
                if (item.Elements(PdfASchema + "namespaceURI")
                        .Any(element => string.Equals(element.Value.Trim(), namespaceUri, StringComparison.Ordinal)) ||
                    item.Attributes(PdfASchema + "namespaceURI")
                        .Any(attribute => string.Equals(attribute.Value.Trim(), namespaceUri, StringComparison.Ordinal)))
                {
                    item.Remove();
                }
            }

            if (!schemas.Descendants(Rdf + "li").Any())
            {
                var owner = schemas.Parent;
                schemas.Remove();
                RemoveIfEmpty(owner);
            }
        }
    }

    private static void RemovePropertyValues(XElement rdf, string namespaceUri)
    {
        XNamespace target = namespaceUri;
        rdf.Descendants()
            .Where(element => element.Name.Namespace == target)
            .ToArray()
            .Remove();
        foreach (var description in rdf.Elements(Rdf + "Description").ToArray())
        {
            description.Attributes()
                .Where(attribute => attribute.Name.Namespace == target)
                .ToArray()
                .Remove();
            RemoveIfEmpty(description);
        }
    }

    private static void RemoveIfEmpty(XElement? description)
    {
        if (description is null || description.HasElements)
        {
            return;
        }

        // rdf:about and namespace declarations alone carry no metadata.
        var meaningful = description.Attributes()
            .Any(attribute => !attribute.IsNamespaceDeclaration && attribute.Name != Rdf + "about");
        if (!meaningful && string.IsNullOrWhiteSpace(description.Value))
        {
            description.Remove();
        }
    }

    private static XElement BuildExtensionSchema(PdfAssociatedFileXmpMetadata metadata) =>
        new(
            Rdf + "Description",
            new XAttribute(Rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + "pdfaExtension", PdfAExtension.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdfaSchema", PdfASchema.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdfaProperty", PdfAProperty.NamespaceName),
            new XElement(
                PdfAExtension + "schemas",
                new XElement(
                    Rdf + "Bag",
                    new XElement(
                        Rdf + "li",
                        new XAttribute(Rdf + "parseType", "Resource"),
                        new XElement(PdfASchema + "schema", metadata.SchemaName),
                        new XElement(PdfASchema + "namespaceURI", metadata.NamespaceUri),
                        new XElement(PdfASchema + "prefix", metadata.Prefix),
                        new XElement(
                            PdfASchema + "property",
                            new XElement(
                                Rdf + "Seq",
                                metadata.Properties.Select(property => new XElement(
                                    Rdf + "li",
                                    new XAttribute(Rdf + "parseType", "Resource"),
                                    new XElement(PdfAProperty + "name", property.Name),
                                    new XElement(PdfAProperty + "valueType", property.ValueType),
                                    new XElement(PdfAProperty + "category", property.Category),
                                    new XElement(PdfAProperty + "description", property.Description)))))))));

    private static XElement BuildPropertyValues(PdfAssociatedFileXmpMetadata metadata)
    {
        XNamespace target = metadata.NamespaceUri;
        return new XElement(
            Rdf + "Description",
            new XAttribute(Rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + metadata.Prefix, metadata.NamespaceUri),
            metadata.Properties.Select(property => new XElement(target + property.Name, property.Value)));
    }

    private static byte[] Serialize(XDocument document)
    {
        var builder = new StringBuilder();
        builder.Append("<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            Encoding = new UTF8Encoding(false)
        }))
        {
            document.Save(writer);
        }

        builder.Append("\n<?xpacket end=\"w\"?>\n");
        return new UTF8Encoding(false).GetBytes(builder.ToString());
    }
}
