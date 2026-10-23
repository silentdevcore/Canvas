using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PVAD.Generator;
using PVAD.Importer.Trust;
using PVAD.Importer.Redaction;
using PVAD.Importer.Parsing;
using PVAD.Importer.Xref;
using PVAD.Pdf;
using PVAD.Core.Contracts;
using PVAD.Core.Primitives;
using PVAD.Infrastructure.Pdf.Charts;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.Api.Tests;

public sealed class PvadEvidenceDiagnosticSerializationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private JsonSerializerOptions ApiOptions => factory.Services
        .GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;

    public static IEnumerable<object[]> AdditionalDiagnostics()
    {
        yield return [new PdfRedactionFinding("PXAPDFREDACT101", PdfRedactionCapability.ReviewRequired, "Synthetic", 2, 1)];
        yield return [new PdfComplianceDiagnostic("PXAPDFA001", PdfComplianceDiagnosticSeverity.Error, "Synthetic", 2)];
        yield return [new PdfAccessibilityDiagnostic("PXAPDFUA001", PdfAccessibilityDiagnosticSeverity.Error, "Synthetic", 2,
            LocalizationKey: "diagnostics.PXAPDFUA001")];
        yield return [new PdfComplianceConversionFinding("PXAPDFACONVERT004", PdfTrustDiagnosticSeverity.Warning, "Synthetic", ObjectNumber: 12)];
        yield return [new PdfParseDiagnostic("PXAPDFPARSE001", "Synthetic", 12, 0)];
        yield return [new PdfCrossReferenceDiagnostic("PXAPDFXREF001", "Synthetic", 123)];
        yield return [new PdfComplianceConversionFinding("PXAEINVATTACH001", PdfTrustDiagnosticSeverity.Warning, "Synthetic", PageNumber: 2)];
        yield return [new ImportDiagnosticDto { Code = "PXALAYOUT001", Source = "page-2", Message = "Synthetic" }];
        yield return [new PvadUsageResult(false, false, "PXA_USAGE_INVALID", "Synthetic", null)];
        yield return [new PvadUsageResult(true, false, "PXA_USAGE_RECORDED", "Synthetic", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))];
        yield return [new PvadUsageResult(true, true, "PXA_USAGE_ALREADY_RECORDED", "Synthetic", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))];
    }

    [Theory]
    [InlineData("PXACHART001: Chart contains no renderable data.", "PVADCHART001")]
    [InlineData("PXACHART002: Vector rendering fell back to raster (Synthetic).", "PVADCHART002")]
    [InlineData("PXACHART999: Future diagnostic.", null)]
    [InlineData("prefix PXACHART001: Not a chart code.", null)]
    [InlineData(null, null)]
    public void ChartStrings_RetainOriginalTextAndOrdinarySerialization(string? diagnostic, string? expected)
    {
        var result = new ChartRenderResult(ChartRenderMode.Empty, diagnostic);
        var retained = JsonSerializer.Serialize(result);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(result, ApiOptions));
        Assert.Equal(diagnostic, wire.RootElement.GetProperty("diagnostic").GetString());
        if (expected is null)
            Assert.False(wire.RootElement.TryGetProperty("canonicalCode", out _));
        else
            Assert.Equal(expected, wire.RootElement.GetProperty("canonicalCode").GetString());
        Assert.Equal(retained, JsonSerializer.Serialize(result));
        Assert.DoesNotContain("canonicalCode", retained);
    }

    [Fact]
    public void LayoutValidation_AliasesAreApiOnlyAndDoNotChangeNormalization()
    {
        var layout = new PageLayoutDto { Width = -1, Height = -2, Orientation = "unknown", BleedSize = -1,
            Margins = new MarginsDto { Top = -1 } };
        var diagnostics = PageLayoutResolver.Validate(null, layout, "page-2");
        var before = JsonSerializer.Serialize(diagnostics);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(diagnostics, ApiOptions));
        Assert.Equal(diagnostics.Count, wire.RootElement.GetArrayLength());
        foreach (var item in wire.RootElement.EnumerateArray())
        {
            var code = item.GetProperty("code").GetString()!;
            Assert.Equal("PVAD" + code[3..], item.GetProperty("canonicalCode").GetString());
            Assert.Equal("page-2", item.GetProperty("source").GetString());
        }
        Assert.Equal(before, JsonSerializer.Serialize(diagnostics));
        Assert.Equal(595, PageLayoutResolver.Resolve(null, layout).Width);
        Assert.Equal(842, PageLayoutResolver.Resolve(null, layout).Height);
    }

    [Theory]
    [MemberData(nameof(AdditionalDiagnostics))]
    public void AdditionalPdfDiagnostics_AddOnlyAnApiAlias(object diagnostic)
    {
        var normal = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var retained = JsonSerializer.Serialize(diagnostic, diagnostic.GetType(), normal);
        using var original = JsonDocument.Parse(retained);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(diagnostic, diagnostic.GetType(), ApiOptions));
        var code = original.RootElement.GetProperty("code").GetString()!;
        Assert.Equal("PVAD" + code[3..], wire.RootElement.GetProperty("canonicalCode").GetString());
        foreach (var property in original.RootElement.EnumerateObject())
            Assert.Equal(property.Value.GetRawText(), wire.RootElement.GetProperty(property.Name).GetRawText());
        Assert.Equal(original.RootElement.EnumerateObject().Count() + 1, wire.RootElement.EnumerateObject().Count());
        Assert.Equal(retained, JsonSerializer.Serialize(diagnostic, diagnostic.GetType(), normal));
        Assert.DoesNotContain("canonicalCode", retained);
    }

    [Fact]
    public void InvoiceAlias_IsAnApiProjection_NotAnEvidenceModelChange()
    {
        var diagnostic = new PvadEInvoiceDiagnostic("PXAEINV100", "error", "Synthetic validation finding")
        {
            OriginalFinding = new("schematron", "BR-CO-10", "fatal", "/Invoice",
                "Original rule wording", "sum(lines) = total"),
        };
        var normal = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var retained = JsonSerializer.Serialize(diagnostic, normal);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(diagnostic, ApiOptions));
        Assert.Equal("PXAEINV100", wire.RootElement.GetProperty("code").GetString());
        Assert.Equal("PVADEINV100", wire.RootElement.GetProperty("canonicalCode").GetString());
        Assert.Equal("BR-CO-10", wire.RootElement.GetProperty("originalFinding").GetProperty("ruleId").GetString());
        Assert.Equal(retained, JsonSerializer.Serialize(diagnostic, normal));
        Assert.DoesNotContain("canonicalCode", retained);
        Assert.Equal("Original rule wording", diagnostic.OriginalFinding.Message);
    }

    [Fact]
    public void TrustAlias_PreservesSeverityPositionLocalizationAndStoredJson()
    {
        var diagnostic = new PdfTrustDiagnostic("PXAPDFTRUST001", PdfTrustDiagnosticSeverity.Error,
            "Synthetic trust finding", ObjectNumber: 12, PageNumber: 2, LocalizationKey: "PXAPDFTRUST001");
        var normal = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var retained = JsonSerializer.Serialize(diagnostic, normal);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(diagnostic, ApiOptions));
        Assert.Equal("PVADPDFTRUST001", wire.RootElement.GetProperty("canonicalCode").GetString());
        Assert.Equal("PXAPDFTRUST001", wire.RootElement.GetProperty("code").GetString());
        Assert.Equal("PXAPDFTRUST001", wire.RootElement.GetProperty("localizationKey").GetString());
        Assert.Equal(12, wire.RootElement.GetProperty("objectNumber").GetInt32());
        Assert.Equal(retained, JsonSerializer.Serialize(diagnostic, normal));
        Assert.DoesNotContain("canonicalCode", retained);
    }

    [Fact]
    public void NestedInvoiceReport_ReceivesAliasWithoutMutation()
    {
        var report = new PvadEInvoiceValidationReport(false, false, "UBL", null,
            [new("PXAEINV001", "error", "Synthetic")]);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(report, ApiOptions));
        Assert.Equal("PVADEINV001", wire.RootElement.GetProperty("diagnostics")[0].GetProperty("canonicalCode").GetString());
        Assert.False(report.IsConformanceValidated);
        Assert.Equal("PXAEINV001", report.Diagnostics[0].Code);
    }

    [Fact]
    public void UnknownRuleIds_AreNeitherRewrittenNorGivenAnAlias()
    {
        var diagnostic = new PvadEInvoiceDiagnostic("BR-CO-10", "error", "Original finding");
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(diagnostic, ApiOptions));
        Assert.Equal("BR-CO-10", wire.RootElement.GetProperty("code").GetString());
        Assert.False(wire.RootElement.TryGetProperty("canonicalCode", out _));
    }

    [Fact]
    public void IncomingAlias_CannotChangeTheDiagnosticCode()
    {
        var diagnostic = JsonSerializer.Deserialize<PvadEInvoiceDiagnostic>(
            """{"code":"PXAEINV001","severity":"error","message":"Synthetic","canonicalCode":"PVADEINV002"}""", ApiOptions)!;
        Assert.Equal("PXAEINV001", diagnostic.Code);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(diagnostic, ApiOptions));
        Assert.Equal("PVADEINV001", wire.RootElement.GetProperty("canonicalCode").GetString());
    }

    [Fact]
    public async Task OpenApi_ExposesProjectionFieldsForBothDiagnosticTypes()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "PvadEInvoiceDiagnostic", "PdfTrustDiagnostic" })
            Assert.True(schemas.GetProperty(name).GetProperty("properties").TryGetProperty("canonicalCode", out _));
    }
}
