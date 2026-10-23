using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Generator;
using PVAD.Importer.Trust;
using PVAD.Importer.Redaction;
using PVAD.Importer.Parsing;
using PVAD.Importer.Xref;
using PVAD.Pdf;
using PVAD.Infrastructure.Pdf.Charts;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.WebApi.Infrastructure;

public static class PvadDiagnosticJsonContract
{
    public static void Configure(JsonSerializerOptions options)
    {
        var resolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = resolver.WithAddedModifier(AddAliases);
    }

    public static void AddAliases(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object ||
            !SupportedTypes.Contains(typeInfo.Type))
            return;

        var alias = typeInfo.CreateJsonPropertyInfo(typeof(string), "canonicalCode");
        alias.Get = value => value switch
        {
            PvadEInvoiceDiagnostic invoice => PvadDiagnosticAliases.CanonicalCode(invoice.Code),
            PdfTrustDiagnostic trust => PvadDiagnosticAliases.CanonicalCode(trust.Code),
            PdfRedactionFinding finding => PvadDiagnosticAliases.CanonicalCode(finding.Code),
            PdfComplianceDiagnostic compliance => PvadDiagnosticAliases.CanonicalCode(compliance.Code),
            PdfAccessibilityDiagnostic accessibility => PvadDiagnosticAliases.CanonicalCode(accessibility.Code),
            PdfComplianceConversionFinding conversion => PvadDiagnosticAliases.CanonicalCode(conversion.Code),
            PdfParseDiagnostic parse => PvadDiagnosticAliases.CanonicalCode(parse.Code),
            PdfCrossReferenceDiagnostic crossReference => PvadDiagnosticAliases.CanonicalCode(crossReference.Code),
            ImportDiagnosticDto import => PvadDiagnosticAliases.CanonicalCode(import.Code),
            ChartRenderResult chart => ChartAlias(chart.Diagnostic),
            PvadUsageResult usage => PvadDiagnosticAliases.CanonicalCode(usage.Code),
            _ => null,
        };
        alias.ShouldSerialize = (_, value) => value is not null;
        typeInfo.Properties.Add(alias);
    }

    private static readonly HashSet<Type> SupportedTypes =
    [
        typeof(PvadEInvoiceDiagnostic), typeof(PdfTrustDiagnostic),
        typeof(PdfRedactionFinding), typeof(PdfComplianceDiagnostic),
        typeof(PdfAccessibilityDiagnostic), typeof(PdfComplianceConversionFinding),
        typeof(PdfParseDiagnostic), typeof(PdfCrossReferenceDiagnostic),
        typeof(ImportDiagnosticDto),
        typeof(ChartRenderResult),
        typeof(PvadUsageResult),
    ];

    private static string? ChartAlias(string? diagnostic) => diagnostic switch
    {
        { } text when text.StartsWith("PXACHART001: ", StringComparison.Ordinal) => PvadDiagnosticAliases.CanonicalCode("PXACHART001"),
        { } text when text.StartsWith("PXACHART002: ", StringComparison.Ordinal) => PvadDiagnosticAliases.CanonicalCode("PXACHART002"),
        _ => null,
    };
}
