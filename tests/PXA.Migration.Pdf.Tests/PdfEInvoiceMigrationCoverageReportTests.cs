using System.Text;
using PXA.Migration.Abstractions;
using PXA.Migration.Pdf;

namespace PXA.Migration.Pdf.Tests;

/// <summary>
/// Keeps the published provider coverage report identical to the catalog. The table between the
/// generated markers is rendered from <see cref="PdfEInvoiceMigrationCatalog"/>; if they differ the
/// test rewrites the document, so the report can never quietly fall behind the code.
/// </summary>
public sealed class PdfEInvoiceMigrationCoverageReportTests
{
    private const string BeginMarker = "<!-- BEGIN GENERATED: e-invoice-provider-coverage -->";
    private const string EndMarker = "<!-- END GENERATED: e-invoice-provider-coverage -->";

    [Fact]
    public void The_published_coverage_report_matches_the_catalog()
    {
        var path = ResolveReportPath();
        var content = File.ReadAllText(path);
        var begin = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = content.IndexOf(EndMarker, StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin, "The coverage report is missing its generated block.");

        var expected = RenderTable();
        var actual = content[(begin + BeginMarker.Length)..end];
        if (!string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal))
        {
            File.WriteAllText(
                path,
                content[..(begin + BeginMarker.Length)] + expected + content[end..]);
            Assert.Fail(
                $"The coverage report at '{path}' was out of date and has been regenerated. " +
                "Review the diff and commit it.");
        }
    }

    [Fact]
    public void The_rendered_table_states_a_disposition_for_every_provider_and_capability()
    {
        var table = RenderTable();

        foreach (var profile in PdfEInvoiceMigrationCatalog.All)
        {
            Assert.Contains($"`{profile.ProviderKey}`", table, StringComparison.Ordinal);
        }
        foreach (var capability in Enum.GetValues<PdfEInvoiceCapability>())
        {
            Assert.Contains(Header(capability), table, StringComparison.Ordinal);
        }

        var rows = table.Split('\n').Count(line => line.StartsWith("| `", StringComparison.Ordinal));
        Assert.Equal(PdfEInvoiceMigrationCatalog.All.Count, rows);
        // Every cell is one of the three dispositions; nothing is blank or a placeholder.
        Assert.DoesNotContain("| |", table, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", table, StringComparison.Ordinal);
    }

    private static string RenderTable()
    {
        var capabilities = Enum.GetValues<PdfEInvoiceCapability>();
        var builder = new StringBuilder("\n\n| Provider |");
        foreach (var capability in capabilities)
        {
            builder.Append(' ').Append(Header(capability)).Append(" |");
        }

        builder.Append("\n|---|");
        for (var index = 0; index < capabilities.Length; index++)
        {
            builder.Append("---|");
        }

        builder.Append('\n');
        foreach (var profile in PdfEInvoiceMigrationCatalog.All)
        {
            builder.Append("| `").Append(profile.ProviderKey).Append('`');
            foreach (var capability in capabilities)
            {
                builder.Append(" | ").Append(Disposition(profile.Capability(capability).Disposition));
            }

            builder.Append(" |\n");
        }

        return builder.ToString();
    }

    private static string Header(PdfEInvoiceCapability capability) => capability switch
    {
        PdfEInvoiceCapability.FacturX => "Factur-X",
        PdfEInvoiceCapability.Zugferd => "ZUGFeRD",
        PdfEInvoiceCapability.XRechnung => "XRechnung",
        PdfEInvoiceCapability.PdfA3Attachment => "PDF/A-3 attachment",
        PdfEInvoiceCapability.InvoiceExtraction => "Extraction",
        PdfEInvoiceCapability.InvoiceValidation => "Validation",
        _ => capability.ToString()
    };

    private static string Disposition(PdfTrustMigrationDisposition disposition) => disposition switch
    {
        PdfTrustMigrationDisposition.Automatic => "automatic",
        PdfTrustMigrationDisposition.ReviewRequired => "reviewRequired",
        _ => "unsupported"
    };

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static string ResolveReportPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "guides", "PXA.Unified-Migration-Provider-Coverage.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "docs/guides/PXA.Unified-Migration-Provider-Coverage.md was not found.");
    }
}
