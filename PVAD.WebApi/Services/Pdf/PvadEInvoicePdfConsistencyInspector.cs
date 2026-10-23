using System.Globalization;
using System.Text;
using PVAD.Generator;
using PVAD.Importer;
using PVAD.Importer.Graphics;

namespace PVAD.WebApi.Services.Pdf;

public sealed record PvadEInvoiceConsistencyFinding(
    string Field,
    string? ExpectedValue,
    string Status,
    string Message);

public sealed record PvadEInvoicePdfConsistencyReport(
    string Status,
    IReadOnlyList<PvadEInvoiceConsistencyFinding> Findings);

public static class PvadEInvoicePdfConsistencyInspector
{
    private const int MaximumExtractedCharacters = 2_000_000;

    public static async Task<string?> ExtractVisibleTextAsync(
        ReadOnlyMemory<byte> pdf,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(pdf.ToArray(), writable: false);
            var document = await new PdfImporter().LoadAsync(stream, cancellationToken);
            var builder = new StringBuilder();
            foreach (var page in document.Pages)
            {
                foreach (var element in page.GraphicsObjects)
                {
                    AppendText(element, builder);
                    if (builder.Length >= MaximumExtractedCharacters)
                        return builder.ToString(0, MaximumExtractedCharacters);
                }
            }
            return builder.Length == 0 ? null : builder.ToString();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public static PvadEInvoicePdfConsistencyReport Inspect(
        string? visibleText,
        PvadEInvoiceSemanticData? data)
    {
        if (data is null || string.IsNullOrWhiteSpace(visibleText))
        {
            return new("notEvaluated",
            [
                new("document", null, "notCheckable", "No extractable PDF text was available for comparison.")
            ]);
        }

        var findings = new List<PvadEInvoiceConsistencyFinding>();
        CheckText("invoiceNumber", data.InvoiceNumber, visibleText, findings);
        CheckText("seller", data.Seller.Name, visibleText, findings);
        CheckText("buyer", data.Buyer.Name, visibleText, findings);
        CheckText("currency", data.Currency, visibleText, findings);
        CheckText("issueDate", data.IssueDate, visibleText, findings);
        CheckAmount("taxExclusiveTotal", data.TaxExclusiveTotal, visibleText, findings);
        CheckAmount("taxTotal", data.TaxTotal, visibleText, findings);
        CheckAmount("taxInclusiveTotal", data.TaxInclusiveTotal, visibleText, findings);
        CheckAmount("payableAmount", data.PayableAmount, visibleText, findings);
        foreach (var (line, index) in data.Lines.Take(100).Select(static (line, index) => (line, index)))
        {
            CheckText($"lines[{index}].identifier", line.Identifier, visibleText, findings);
            CheckText($"lines[{index}].description", line.Description, visibleText, findings);
            CheckAmount($"lines[{index}].netAmount", line.NetAmount, visibleText, findings);
        }

        var checkedFindings = findings.Where(static item => item.Status != "notCheckable").ToArray();
        var status = checkedFindings.Length == 0
            ? "notEvaluated"
            : checkedFindings.Any(static item => item.Status == "notFound")
                ? "reviewRequired"
                : "consistent";
        return new(status, findings);
    }

    private static void AppendText(PdfGraphicsElement element, StringBuilder builder)
    {
        switch (element)
        {
            case PdfTextElement text when !text.IsDeleted && !string.IsNullOrWhiteSpace(text.Text):
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(text.Text);
                break;
            case PdfGroupElement group when !group.IsDeleted:
                foreach (var child in group.Children) AppendText(child, builder);
                break;
        }
    }

    private static void CheckText(
        string field,
        string? value,
        string visibleText,
        ICollection<PvadEInvoiceConsistencyFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            findings.Add(new(field, null, "notCheckable", "The invoice XML does not provide this value."));
            return;
        }

        var found = Normalize(visibleText).Contains(Normalize(value), StringComparison.Ordinal);
        findings.Add(new(
            field,
            value,
            found ? "confirmed" : "notFound",
            found
                ? "The XML value was found in the visible PDF text."
                : "The XML value was not found in the extractable PDF text; visual review is required."));
    }

    private static void CheckAmount(
        string field,
        decimal? value,
        string visibleText,
        ICollection<PvadEInvoiceConsistencyFinding> findings)
    {
        if (value is null)
        {
            findings.Add(new(field, null, "notCheckable", "The invoice XML does not provide this amount."));
            return;
        }

        var variants = AmountVariants(value.Value);
        var normalizedText = NormalizeAmountWhitespace(visibleText);
        var found = variants.Any(variant => ContainsNumberToken(
            normalizedText,
            NormalizeAmountWhitespace(variant)));
        var expected = value.Value.ToString(
            "F" + DecimalScale(value.Value),
            CultureInfo.InvariantCulture);
        findings.Add(new(
            field,
            expected,
            found ? "confirmed" : "notFound",
            found
                ? "The XML amount was found in the visible PDF text."
                : "The XML amount was not found in the extractable PDF text; visual review is required."));
    }

    /// <summary>
    /// How the same amount may legitimately appear on a page. Two minor digits are not a safe
    /// assumption: a currency without minor units prints "4502" and a three-digit currency prints
    /// "24.691", so the value's own scale has to be offered alongside the two-digit forms. Without
    /// it, a correct invoice in such a currency is reported as needing review.
    /// </summary>
    private static IReadOnlySet<string> AmountVariants(decimal value)
    {
        var variants = new HashSet<string>(StringComparer.Ordinal)
        {
            value.ToString("0.############################", CultureInfo.InvariantCulture)
        };
        foreach (var places in new[] { 2, DecimalScale(value) }.Distinct())
        {
            variants.Add(value.ToString("F" + places, CultureInfo.InvariantCulture));
            foreach (var culture in AmountCultures)
            {
                variants.Add(value.ToString("N" + places, culture));
            }
        }

        return variants;
    }

    private static readonly CultureInfo[] AmountCultures =
    [
        CultureInfo.GetCultureInfo("en-US"),
        CultureInfo.GetCultureInfo("de-DE"),
        CultureInfo.GetCultureInfo("fr-FR")
    ];

    /// <summary>The number of decimal places the value carries, 0-28.</summary>
    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private static string NormalizeAmountWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsWhiteSpace(character) ? ' ' : character);
        }
        return builder.ToString();
    }

    private static bool ContainsNumberToken(string text, string value)
    {
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            var beforeIsDigit = index > 0 && char.IsDigit(text[index - 1]);
            var end = index + value.Length;
            var afterIsDigit = end < text.Length && char.IsDigit(text[end]);
            if (!beforeIsDigit && !afterIsDigit) return true;
            index++;
        }
        return false;
    }
}
