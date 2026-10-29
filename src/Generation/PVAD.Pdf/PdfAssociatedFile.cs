namespace PVAD.Pdf;

/// <summary>Relationship between an associated file and the PDF document.</summary>
public enum PdfAssociatedFileRelationship
{
    Source,
    Data,
    Alternative,
    Supplement,
    Unspecified
}

/// <summary>An embedded file associated with the complete PDF document.</summary>
public sealed class PdfAssociatedFile
{
    private readonly byte[] _content;

    internal PdfAssociatedFile(
        string fileName,
        string mimeType,
        byte[] content,
        PdfAssociatedFileRelationship relationship,
        string? description,
        PdfAssociatedFileXmpMetadata? xmpMetadata)
    {
        FileName = fileName;
        MimeType = mimeType;
        _content = content;
        Relationship = relationship;
        Description = description;
        XmpMetadata = xmpMetadata;
    }

    public string FileName { get; }

    public string MimeType { get; }

    public ReadOnlyMemory<byte> Content => _content.ToArray();

    public PdfAssociatedFileRelationship Relationship { get; }

    public string? Description { get; }

    /// <summary>Optional PDF/A extension metadata describing this associated file.</summary>
    public PdfAssociatedFileXmpMetadata? XmpMetadata { get; }

    internal byte[] ContentBytes => _content;
}

/// <summary>A property declared by a PDF/A XMP extension schema.</summary>
public sealed record PdfAssociatedFileXmpProperty(
    string Name,
    string Value,
    string ValueType,
    string Category,
    string Description);

/// <summary>
/// A bounded PDF/A XMP extension schema and its document-level values. Callers must use
/// specifications they control or have independently validated.
/// </summary>
public sealed record PdfAssociatedFileXmpMetadata
{
    public required string SchemaName { get; init; }
    public required string NamespaceUri { get; init; }
    public required string Prefix { get; init; }
    public required IReadOnlyList<PdfAssociatedFileXmpProperty> Properties { get; init; }
}
