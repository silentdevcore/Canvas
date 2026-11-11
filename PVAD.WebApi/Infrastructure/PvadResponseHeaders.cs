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
            .Select(suffix => $"X-PVAD-{suffix}"))
        .Concat(DocumentSuffixes.Select(suffix => $"PVAD-{suffix}"))
        .ToArray();

    public static void SetDocument(HttpResponse response, string suffix, string value)
    {
        response.Headers[$"PVAD-{suffix}"] = value;
    }

    public static void Set(HttpResponse response, string suffix, string value)
    {
        response.Headers[$"X-PVAD-{suffix}"] = value;
    }
}
