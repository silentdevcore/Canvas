using System.Text.Json;
using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Pdf.Tests;

public sealed class PdfEInvoiceRenameAliasTests
{
    [Theory]
    [InlineData("pvadEquivalent")]
    [InlineData("pxaEquivalent")]
    public void Capability_reads_each_name_and_writes_matching_values_for_both_generations(string property)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = $$"""
            {"capability":0,"disposition":0,"rationale":"Synthetic mapping","documentationPath":"/docs/example","{{property}}":"PVAD.Generator.EInvoice"}
            """;
        var model = JsonSerializer.Deserialize<PdfEInvoiceProviderCapability>(json, options)!;
        Assert.Equal("PVAD.Generator.EInvoice", model.PvadEquivalent);
        using var output = JsonDocument.Parse(JsonSerializer.Serialize(model, options));
        Assert.Equal(output.RootElement.GetProperty("pvadEquivalent").GetString(),
            output.RootElement.GetProperty("pxaEquivalent").GetString());
    }

    [Theory]
    [InlineData("pvadEquivalent")]
    [InlineData("pxaEquivalent")]
    public void Finding_reads_each_name_without_losing_diagnostics_or_source_markers(string property)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = $$"""
            {"providerKey":"synthetic","capability":0,"disposition":0,"code":"CANMIGEINV001","matchedMarkers":["vendor.Api"],"rationale":"Synthetic mapping","documentationPath":"/docs/example","{{property}}":"PVAD.Generator.EInvoice"}
            """;
        var model = JsonSerializer.Deserialize<PdfEInvoiceMigrationFinding>(json, options)!;
        Assert.Equal("PVAD.Generator.EInvoice", model.PvadEquivalent);
        Assert.Equal("CANMIGEINV001", model.Code);
        Assert.Equal("vendor.Api", Assert.Single(model.MatchedMarkers));
        using var output = JsonDocument.Parse(JsonSerializer.Serialize(model, options));
        Assert.Equal(output.RootElement.GetProperty("pvadEquivalent").GetString(),
            output.RootElement.GetProperty("pxaEquivalent").GetString());
    }
}
