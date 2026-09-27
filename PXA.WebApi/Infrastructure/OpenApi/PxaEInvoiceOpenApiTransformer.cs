using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PXA.WebApi.Infrastructure.OpenApi;

/// <summary>
/// Documents the electronic-invoice API in the generated OpenAPI document: what each operation
/// does, which approvals and limits it enforces, and - most importantly - that structural
/// recognition, PXA business validation and official conformance are three separate states.
/// </summary>
public sealed class PxaEInvoiceOpenApiTransformer : IOpenApiDocumentTransformer
{
    private const string RoutePrefix = "/api/pxa/v1/pdf/e-invoices";

    /// <summary>
    /// Repeated on every operation because conflating these three states is the single most
    /// damaging mistake a caller can make with an invoice API.
    /// </summary>
    internal const string ConformanceStates = """

        ### Validation states

        PXA reports three independent states and never merges them:

        | State | Field | Meaning |
        | --- | --- | --- |
        | Structural recognition | `isStructurallyValid` | The XML parsed under a bounded, DTD-free reader and was recognised as CII or UBL with a declared guideline identifier. It is **not** a conformance claim. |
        | PXA business validation | `diagnostics` with `PXAEINV...` codes | PXA's own checks, for example profile/standard agreement and arithmetic totals. |
        | Official conformance | `isConformanceValidated`, `conformanceStatus` | `validated`, `invalid` or `notEvaluated`, produced only by a pinned official XSD/Schematron artifact set that is identified by `validatorArtifactSetId` and `validatorArtifactSetSha256`. `notEvaluated` means no artifact set was configured for that standard, version, profile and syntax. |

        An invoice is never reported as conformant on the strength of structural validation alone.
        """;

    private static readonly Dictionary<string, (string Summary, string Description)> Operations = new(StringComparer.Ordinal)
    {
        [$"{RoutePrefix}/generate"] = (
            "Generate invoice XML from typed invoice data",
            "Produces deterministic CII (Factur-X, ZUGFeRD) or UBL (XRechnung) XML from a typed invoice document. Tax groups and totals are calculated by PXA. Requires the `designer.pdf.e-invoices.generate` permission and the `generator` product entitlement. The JSON request body is limited to 2 MiB."),
        [$"{RoutePrefix}/create"] = (
            "Create a visible PDF/A-3 hybrid invoice",
            "Renders a visible invoice page and embeds the generated XML as a PDF/A-3 associated file, so the rendered content and the embedded semantic values agree. Requires the `designer.pdf.e-invoices.create` permission and the `generator` entitlement. Limited to 2 000 invoice lines and a 50 MiB generated PDF."),
        [$"{RoutePrefix}/attach"] = (
            "Attach invoice XML to an existing PDF",
            "Embeds validated invoice XML into a PDF that already exists. A source that already identifies PDF/A-3b receives an incremental update, so the original bytes are the prefix of the result and no visible page can change. A source whose archival identification forbids embedded files is converted to PDF/A-3b first, which rewrites the file and therefore requires `allowArchiveConversion=true`. Modifying a signed document requires `allowSignatureInvalidation=true`: a rewrite removes its signatures, while an incremental attachment preserves the signed bytes but adds a later revision. Replacing an existing invoice attachment (`replaceExistingInvoice`) and attaching an invoice whose values were not found in the visible page (`allowInconsistentPdf`) each require their own explicit approval. Blocked operations return `409` with the full preflight report and never modify the source. Requires the `designer.pdf.e-invoices.attach` permission and the `generator` entitlement; the source PDF is limited to 100 MiB, the invoice XML to 10 MiB and the result to 120 MiB."),
        [$"{RoutePrefix}/attach/preflight"] = (
            "Report what attaching an invoice would do",
            "Runs every attachment check without producing a document: archival profile, required conversion, signatures, conflicting attachments, XMP readability, estimated output size, invoice validation and PDF/XML semantic consistency. Requires the same permission as `attach`, because the report describes the document's content."),
        [$"{RoutePrefix}/validate"] = (
            "Validate invoice XML",
            "Validates uploaded invoice XML against the declared standard, version and profile. Requires the `designer.pdf.e-invoices.validate` permission. The upload is limited to 10 MiB; DTDs, external entities and unsupported syntaxes are rejected."),
        [$"{RoutePrefix}/inspect"] = (
            "Inspect the invoice attachments of a PDF",
            "Lists every document-associated file, its integrity state, the recognised standard, syntax, profile and semantic values, and a conservative comparison against the visible PDF text. Requires the `designer.pdf.e-invoices.read` permission. The upload is limited to 100 MiB."),
        [$"{RoutePrefix}/extract"] = (
            "Extract one invoice attachment byte for byte",
            "Returns one associated file exactly as it is stored, after verifying its declared size and PDF checksum. Requires the `designer.pdf.e-invoices.read` permission.")
    };

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (path, item) in document.Paths)
        {
            if (!Operations.TryGetValue(path, out var text) || item.Operations is null)
            {
                continue;
            }

            foreach (var operation in item.Operations.Values)
            {
                operation.Summary = text.Summary;
                operation.Description = text.Description + ConformanceStates;
                if (path.EndsWith("/generate", StringComparison.Ordinal) ||
                    path.EndsWith("/create", StringComparison.Ordinal))
                {
                    AddGenerationExamples(operation);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void AddGenerationExamples(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is not { } content ||
            !content.TryGetValue("application/json", out var media))
        {
            return;
        }

        media.Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal)
        {
            ["facturX"] = Example(
                "Factur-X 1.09.2, EN 16931 profile",
                "A French/European hybrid invoice in UN/CEFACT CII syntax.",
                "FacturX",
                "1.09.2",
                "EN 16931"),
            ["zugferd"] = Example(
                "ZUGFeRD 2.5.2, EXTENDED profile",
                "The German product identity of the same technical hybrid format. Its XML is embedded as factur-x.xml and uses the Factur-X XMP namespace.",
                "ZUGFeRD",
                "2.5.2",
                "EXTENDED"),
            ["xrechnung"] = Example(
                "XRechnung 3.0.2",
                "German public-sector invoicing in UBL syntax. Embedding it in a PDF is a PXA workflow, not a requirement of the standard.",
                "XRechnung",
                "3.0.2",
                "XRECHNUNG")
        };
    }

    private static OpenApiExample Example(
        string summary,
        string description,
        string standard,
        string version,
        string profile) => new()
        {
            Summary = summary,
            Description = description,
            Value = new JsonObject
            {
                ["standard"] = standard,
                ["version"] = version,
                ["profile"] = profile,
                ["invoice"] = new JsonObject
                {
                    ["invoiceNumber"] = "INV-2026-000042",
                    ["issueDate"] = "2026-09-26",
                    ["dueDate"] = "2026-10-26",
                    ["currency"] = "EUR",
                    ["seller"] = new JsonObject
                    {
                        ["name"] = "Seller GmbH",
                        ["vatIdentifier"] = "DE123456789",
                        ["address"] = new JsonObject
                        {
                            ["street"] = "Main Street 1",
                            ["city"] = "Berlin",
                            ["postalCode"] = "10115",
                            ["countryCode"] = "DE"
                        }
                    },
                    ["buyer"] = new JsonObject
                    {
                        ["name"] = "Buyer AG",
                        ["address"] = new JsonObject
                        {
                            ["street"] = "Buyer Street 2",
                            ["city"] = "Munich",
                            ["postalCode"] = "80331",
                            ["countryCode"] = "DE"
                        }
                    },
                    ["lines"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["identifier"] = "1",
                            ["description"] = "PXA Generator subscription",
                            ["quantity"] = 1,
                            ["unitCode"] = "C62",
                            ["netAmount"] = 100.00m,
                            ["taxRatePercent"] = 19.00m
                        }
                    }
                }
            }
        };
}
