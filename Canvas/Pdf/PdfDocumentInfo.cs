namespace Canvas.Pdf;

public sealed class PdfDocumentInfo
{
    private readonly Dictionary<string, string> _customProperties = new(StringComparer.Ordinal);

    public string? Title { get; set; }

    public string? Author { get; set; }

    public string? Subject { get; set; }

    public string? Keywords { get; set; }

    public string? Creator { get; set; }

    public string? Producer { get; set; }

    public DateTimeOffset? CreationDate { get; set; }

    public DateTimeOffset? ModificationDate { get; set; }

    public IDictionary<string, string> CustomProperties => _customProperties;

    internal bool HasValues =>
        !string.IsNullOrWhiteSpace(Title)
        || !string.IsNullOrWhiteSpace(Author)
        || !string.IsNullOrWhiteSpace(Subject)
        || !string.IsNullOrWhiteSpace(Keywords)
        || !string.IsNullOrWhiteSpace(Creator)
        || !string.IsNullOrWhiteSpace(Producer)
        || CreationDate is not null
        || ModificationDate is not null
        || _customProperties.Count > 0;
}
