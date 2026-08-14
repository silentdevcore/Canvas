using PXA.WebApi.Application.Legal;

namespace PXA.Api.Tests;

public sealed class LegalContentCatalogTests
{
    [Fact]
    public void Embedded_candidates_are_English_authoritative_and_Swiss_law_based()
    {
        var catalog = new PxaLegalContentCatalog().Current;

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal("en", catalog.AuthoritativeLocale);
        Assert.Equal("Switzerland", catalog.GoverningLaw);
        Assert.Equal(7, catalog.Documents.Count);
        Assert.All(catalog.Documents, document =>
        {
            Assert.Equal("en", document.Locale);
            Assert.StartsWith("# ", document.SourceMarkdown, StringComparison.Ordinal);
            Assert.True(document.SourceMarkdown.Length > 900);
        });
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("fr-CH", "en")]
    [InlineData("en-US", "en")]
    [InlineData("de-CH", "de")]
    public void English_is_the_default_and_fallback_legal_locale(string? value, string expected)
    {
        Assert.Equal(expected, PxaLegalDocumentService.NormalizeLocale(value));
    }
}
