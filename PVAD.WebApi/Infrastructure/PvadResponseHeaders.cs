namespace PVAD.WebApi.Infrastructure;

public static class PvadResponseHeaders
{
    public static readonly string[] DocumentSuffixes =
    [
        "Source-SHA256", "Result-SHA256", "Compliance-Profile",
        "Redaction-Audit", "Redaction-Audit-Location", "Redaction-Audit-Signed",
        "Redaction-Audit-Signer", "Redaction-Audit-Timestamped", "Redaction-Result-Sha256",
        "Invoice-SHA256", "Invoice-Attachment", "Attachment-Strategy", "Pages-Unchanged",
        "Invoice-Consistency", "Invoice-Conformance", "Invoice-Mode", "Invoice-Recognition",
        "Invoice-Editable", "Validation-Record",
    ];

    public static string[] ExposedHeaders => new[] { "Content-Disposition", "Link" }
        .Concat(new[] { "Template-Revision", "PAdES-Profile", "PDF-Timestamp" }
            .SelectMany(suffix => new[] { $"X-PVAD-{suffix}", $"X-PXA-{suffix}" }))
        .Concat(DocumentSuffixes.SelectMany(suffix => new[] { $"PVAD-{suffix}", $"PXA-{suffix}" }))
        .ToArray();

    public static void SetDocument(HttpResponse response, string suffix, string value)
    {
        response.Headers[$"PVAD-{suffix}"] = value;
        response.Headers[$"PXA-{suffix}"] = value;
    }

    // Preserve old response-header readers for the API compatibility major.
    public static void Set(HttpResponse response, string suffix, string value)
    {
        response.Headers[$"X-PVAD-{suffix}"] = value;
        response.Headers[$"X-PXA-{suffix}"] = value;
    }
}
