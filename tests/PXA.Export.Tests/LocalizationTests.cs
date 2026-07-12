using PXA.Core.Contracts;
using PXA.WebApi.Infrastructure;

namespace PXA.Export.Tests;

/// <summary>
/// Tests for the document localization system: LocalizedPropertyResolver and DesignJsonMapper substitution.
/// Property scopes:
///   "global" — placeholder in ALL language PDFs; each language supplies its own value via LocalizedValues.
///   "own"    — placeholder ONLY in the language identified by OwnerLanguage; excluded from all others.
/// </summary>
public sealed class LocalizationTests
{
    // ── Global property resolution ───────────────────────────────────────────

    [Fact]
    public void Resolve_GlobalProperty_ReturnsLanguageSpecificValue()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "SUBJECT", Scope = "global",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Hallo Welt", ["en"] = "Hello World", ["ar"] = "مرحبا" }
            }
        };

        Assert.Equal("Hallo Welt",  LocalizedPropertyResolver.Resolve(props, "de", "de")["SUBJECT"]);
        Assert.Equal("Hello World", LocalizedPropertyResolver.Resolve(props, "en", "de")["SUBJECT"]);
        Assert.Equal("مرحبا",       LocalizedPropertyResolver.Resolve(props, "ar", "de")["SUBJECT"]);
    }

    [Fact]
    public void Resolve_GlobalProperty_MissingTargetLang_FallsBackToSystemLanguage()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "GREETING", Scope = "global",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Hallo" }
            }
        };
        // "fr" has no value → falls back to system lang "de"
        var result = LocalizedPropertyResolver.Resolve(props, "fr", "de");
        Assert.Equal("Hallo", result["GREETING"]);
    }

    [Fact]
    public void Resolve_GlobalProperty_MissingBothLanguages_ReturnsEmptyString()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "GREETING", Scope = "global",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Hallo" }
            }
        };
        // Neither "fr" nor "it" have a value → empty string (no globalValue fallback)
        var result = LocalizedPropertyResolver.Resolve(props, "fr", "it");
        Assert.Equal("", result["GREETING"]);
    }

    [Fact]
    public void Resolve_GlobalProperty_IncludedForAllLanguages()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "COMPANY", Scope = "global",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Firma GmbH", ["ar"] = "شركة" }
            }
        };
        // Global property must appear in the result for every target language
        var de = LocalizedPropertyResolver.Resolve(props, "de", "de");
        var ar = LocalizedPropertyResolver.Resolve(props, "ar", "de");
        Assert.True(de.ContainsKey("COMPANY"));
        Assert.True(ar.ContainsKey("COMPANY"));
        Assert.Equal("Firma GmbH", de["COMPANY"]);
        Assert.Equal("شركة",       ar["COMPANY"]);
    }

    // ── Own property resolution ──────────────────────────────────────────────

    [Fact]
    public void Resolve_OwnProperty_IncludedOnlyForOwnerLanguage()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "DISCLAIMER", Scope = "own", OwnerLanguage = "de",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Nur auf Deutsch" }
            }
        };

        var de = LocalizedPropertyResolver.Resolve(props, "de", "de");
        var ar = LocalizedPropertyResolver.Resolve(props, "ar", "de");
        var en = LocalizedPropertyResolver.Resolve(props, "en", "de");

        Assert.Equal("Nur auf Deutsch", de["DISCLAIMER"]);
        Assert.False(ar.ContainsKey("DISCLAIMER")); // excluded for AR
        Assert.False(en.ContainsKey("DISCLAIMER")); // excluded for EN
    }

    [Fact]
    public void Resolve_OwnProperty_ReturnsOwnerValue()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "AR_FOOTER", Scope = "own", OwnerLanguage = "ar",
                LocalizedValues = new Dictionary<string, string> { ["ar"] = "تذييل عربي" }
            }
        };

        var ar = LocalizedPropertyResolver.Resolve(props, "ar", "de");
        Assert.Equal("تذييل عربي", ar["AR_FOOTER"]);
    }

    [Fact]
    public void Resolve_OwnProperty_NotVisibleToSystemLanguage_UnlessOwner()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "EN_ONLY", Scope = "own", OwnerLanguage = "en",
                LocalizedValues = new Dictionary<string, string> { ["en"] = "English exclusive" }
            }
        };
        // System language "de" is not the owner — must not appear in DE result
        var de = LocalizedPropertyResolver.Resolve(props, "de", "de");
        Assert.False(de.ContainsKey("EN_ONLY"));
    }

    // ── Mixed global + own ───────────────────────────────────────────────────

    [Fact]
    public void Resolve_MixedProperties_CorrectScoping()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "SUBJECT", Scope = "global",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Betreff", ["en"] = "Subject" }
            },
            new LocalizedPropertyDto
            {
                Key = "DE_DISCLAIMER", Scope = "own", OwnerLanguage = "de",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Nur DE" }
            }
        };

        var de = LocalizedPropertyResolver.Resolve(props, "de", "de");
        var en = LocalizedPropertyResolver.Resolve(props, "en", "de");

        // Both have SUBJECT
        Assert.Equal("Betreff", de["SUBJECT"]);
        Assert.Equal("Subject", en["SUBJECT"]);

        // Only DE has DE_DISCLAIMER
        Assert.Equal("Nur DE", de["DE_DISCLAIMER"]);
        Assert.False(en.ContainsKey("DE_DISCLAIMER"));
    }

    // ── Utilities ────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_EmptyProperties_ReturnsEmptyDictionary()
    {
        Assert.Empty(LocalizedPropertyResolver.Resolve(null, "de", "de"));
        Assert.Empty(LocalizedPropertyResolver.Resolve([], "de", "de"));
    }

    [Fact]
    public void Resolve_NormalizesLanguageTag_IgnoresRegionSubtag()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "MSG", Scope = "global",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Hallo" }
            }
        };
        // "de-DE" should resolve to "de" base tag
        var result = LocalizedPropertyResolver.Resolve(props, "de-DE", "de");
        Assert.Equal("Hallo", result["MSG"]);
    }

    [Fact]
    public void Resolve_OwnProperty_NormalizesOwnerLanguageTag()
    {
        var props = new[]
        {
            new LocalizedPropertyDto
            {
                Key = "LOCAL", Scope = "own", OwnerLanguage = "de-AT",
                LocalizedValues = new Dictionary<string, string> { ["de"] = "Österreichisch" }
            }
        };
        // ownerLanguage "de-AT" normalizes to "de"; target "de" should match
        var de = LocalizedPropertyResolver.Resolve(props, "de", "de");
        Assert.True(de.ContainsKey("LOCAL"));
    }

    // ── ScanPropertyKeys ─────────────────────────────────────────────────────

    [Fact]
    public void ScanPropertyKeys_FindsAllKeys()
    {
        var design = new DesignExportDto
        {
            Id = "1",
            Pages = [
                new PageDto { Id = "p1", Elements = [
                    new ElementDto { Id = "e1", Type = "text", Content = "Hello {{NAME}}, your {{SUBJECT}} is ready." },
                    new ElementDto { Id = "e2", Type = "text", Content = "{{COMPANY}}" },
                ] }
            ],
            SharedElements = [
                new ElementDto { Id = "s1", Type = "text", Content = "{{FOOTER}}" }
            ]
        };

        var keys = LocalizedPropertyResolver.ScanPropertyKeys(design);
        Assert.Contains("NAME", keys);
        Assert.Contains("SUBJECT", keys);
        Assert.Contains("COMPANY", keys);
        Assert.Contains("FOOTER", keys);
        Assert.Equal(4, keys.Count);
    }

    [Fact]
    public void ScanPropertyKeys_ReturnsEmpty_WhenNoPlaceholders()
    {
        var design = new DesignExportDto
        {
            Id = "1",
            Pages = [new PageDto { Id = "p1", Elements = [new ElementDto { Id = "e1", Type = "text", Content = "Plain text" }] }]
        };
        Assert.Empty(LocalizedPropertyResolver.ScanPropertyKeys(design));
    }

    // ── DesignJsonMapper substitution ─────────────────────────────────────────

    [Fact]
    public void MapToPdfDocument_SubstitutesGlobalProperty_ForTargetLanguage()
    {
        var design = new DesignExportDto
        {
            Id = "1", Name = "Test",
            Pages = [
                new PageDto { Id = "p1", Elements = [
                    new ElementDto { Id = "e1", Type = "text", X = 50, Y = 700, Width = 200, Height = 30, Content = "{{SUBJECT}}" }
                ] }
            ],
            PageSettings = new PageSettingsDto
            {
                Width = 595, Height = 842,
                SystemLanguage = "de",
                ActiveLanguages = ["de", "en"],
                LocalizedProperties = [
                    new LocalizedPropertyDto
                    {
                        Key = "SUBJECT", Scope = "global",
                        LocalizedValues = new Dictionary<string, string> { ["de"] = "Betreff", ["en"] = "Subject" }
                    }
                ]
            }
        };

        var docDe = DesignJsonMapper.MapToPdfDocument(design, fontLoader: null, targetLanguage: "de");
        var bytesDe = docDe.ToBytes();
        Assert.True(bytesDe.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytesDe[..4]);

        var docEn = DesignJsonMapper.MapToPdfDocument(design, fontLoader: null, targetLanguage: "en");
        Assert.True(docEn.ToBytes().Length > 0);
    }

    [Fact]
    public void MapToPdfDocument_OwnProperty_OnlySubstitutedForOwnerLanguage()
    {
        var design = new DesignExportDto
        {
            Id = "2", Name = "OwnProp",
            Pages = [
                new PageDto { Id = "p1", Elements = [
                    new ElementDto { Id = "e1", Type = "text", X = 50, Y = 700, Width = 200, Height = 30, Content = "{{DE_NOTE}}" }
                ] }
            ],
            PageSettings = new PageSettingsDto
            {
                Width = 595, Height = 842,
                SystemLanguage = "de",
                ActiveLanguages = ["de", "en"],
                LocalizedProperties = [
                    new LocalizedPropertyDto
                    {
                        Key = "DE_NOTE", Scope = "own", OwnerLanguage = "de",
                        LocalizedValues = new Dictionary<string, string> { ["de"] = "Nur Deutsch" }
                    }
                ]
            }
        };

        // DE export: Own property is included → placeholder substituted
        var docDe = DesignJsonMapper.MapToPdfDocument(design, fontLoader: null, targetLanguage: "de");
        Assert.True(docDe.ToBytes().Length > 0);

        // EN export: Own property excluded → placeholder stays as "{{DE_NOTE}}" (no crash)
        var docEn = DesignJsonMapper.MapToPdfDocument(design, fontLoader: null, targetLanguage: "en");
        Assert.True(docEn.ToBytes().Length > 0);
    }

    [Fact]
    public void MapToPdfDocument_WithNoLocalizedProperties_GeneratesValidPdf()
    {
        var design = new DesignExportDto
        {
            Id = "3", Name = "NoProps",
            Pages = [new PageDto { Id = "p1", Elements = [new ElementDto { Id = "e1", Type = "text", X = 50, Y = 700, Width = 200, Height = 30, Content = "Hello World" }] }]
        };
        var doc = DesignJsonMapper.MapToPdfDocument(design, fontLoader: null, targetLanguage: "de");
        Assert.Equal("%PDF"u8.ToArray(), doc.ToBytes()[..4]);
    }
}
