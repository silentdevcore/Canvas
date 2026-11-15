using System.Security.Cryptography;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Importer;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class RenderingContentIsolationTests
{
    [Fact]
    public async Task RenderingMultipleLanguagesDoesNotChangeTheCanonicalDocumentOrItsChecksum()
    {
        var design = new DesignExportDto
        {
            Id = "synthetic-language-render", Name = "Localized document",
            PageSettings = new PageSettingsDto
            {
                SystemLanguage = "en", LocalizedProperties = [new LocalizedPropertyDto
                {
                    Key = "TITLE", Scope = "global", LocalizedValues = new() { ["en"] = "Hello", ["fr"] = "Bonjour" }
                }]
            },
            Pages = [new PageDto { Id = "page", Elements = [new ElementDto
            {
                Id = "text", Type = "text", Content = "{{TITLE}}", X = 20, Y = 20, Width = 200, Height = 40,
                Style = new() { ["fontSize"] = 12, ["color"] = "#123456" }
            }] }],
            SharedElements = [new ElementDto { Id = "shared", Type = "signature", SignatureLabel = "{{TITLE}}",
                X = 20, Y = 100, Width = 200, Height = 40 }]
        };
        var before = JsonSerializer.SerializeToUtf8Bytes(design);
        var checksum = SHA256.HashData(before);
        foreach (var (language, expected, absent) in new[] { ("fr", "Bonjour", "Hello"), ("en", "Hello", "Bonjour") })
        {
            var bytes = DesignJsonMapper.MapToPdfDocument(design, targetLanguage: language).ToBytes();
            var imported = await new PdfImporter().LoadAsync(new MemoryStream(bytes));
            var text = imported.Pages.SelectMany(page => page.TextObjects).Select(text => text.Text).ToArray();
            Assert.Contains(expected, text);
            Assert.DoesNotContain(absent, text);
            Assert.Equal(before, JsonSerializer.SerializeToUtf8Bytes(design));
            Assert.Equal(checksum, SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(design)));
            Assert.Equal("{{TITLE}}", design.Pages[0].Elements[0].Content);
            Assert.Equal("{{TITLE}}", design.SharedElements[0].SignatureLabel);
        }
    }
}
