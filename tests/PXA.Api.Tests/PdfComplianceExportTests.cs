using PXA.Core.Contracts;
using PXA.Pdf;
using PXA.WebApi.Infrastructure;

namespace PXA.Api.Tests;

public sealed class PdfComplianceExportTests
{
    [Fact]
    public void BuildPdfSaveOptions_MapsPdfUa1AccessibilityProfile()
    {
        var design = new DesignExportDto
        {
            PageSettings = new PageSettingsDto { AccessibilityProfile = "PdfUa1" }
        };

        var options = Assert.IsType<PdfSaveOptions>(DesignJsonMapper.BuildSaveOptions(design));

        Assert.Equal(PdfAccessibilityProfile.PdfUa1, options.AccessibilityProfile);
    }

    [Fact]
    public void DesignerModel_ExportsExplicitSemanticsAsTaggedPdf()
    {
        var design = new DesignExportDto
        {
            Name = "Accessible report",
            PageSettings = new PageSettingsDto
            {
                SystemLanguage = "en-US",
                AccessibilityProfile = "PdfUa1",
                Metadata = new PdfMetadataDto { Title = "Accessible report" }
            },
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "summary-chart",
                            Type = "shape",
                            X = 20,
                            Y = 20,
                            Width = 120,
                            Height = 80,
                            Style = new Dictionary<string, object> { ["backgroundColor"] = "#335577" },
                            Accessibility = new AccessibilityPropertiesDto
                            {
                                Role = "Figure",
                                AlternateText = "Quarterly revenue chart",
                                ReadingOrder = 1
                            }
                        }
                    ]
                }
            ]
        };

        var document = DesignJsonMapper.MapToPdfDocument(
            design,
            new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")));
        var bytes = document.ToBytes(DesignJsonMapper.BuildSaveOptions(design));
        var pdf = System.Text.Encoding.Latin1.GetString(bytes);

        Assert.Contains("/Type /StructTreeRoot", pdf);
        Assert.Contains("/Figure <</MCID 0>> BDC", pdf);
        Assert.Contains("/Alt <FEFF0051007500610072007400650072006C007900200072006500760065006E00750065002000630068006100720074>", pdf);
    }

    [Fact]
    public void DesignerModel_ExportsAccessibleTableLinkAndFormAnnotations()
    {
        var design = new DesignExportDto
        {
            Name = "Accessible interactions",
            PageSettings = new PageSettingsDto
            {
                SystemLanguage = "en-US",
                AccessibilityProfile = "PdfUa1",
                Metadata = new PdfMetadataDto { Title = "Accessible interactions" }
            },
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "orders",
                            Type = "table",
                            X = 20, Y = 20, Width = 300, Height = 80,
                            CellData = [["Item", "Total"], ["Service", "25.00"]],
                            HeaderRow = true,
                            Accessibility = new AccessibilityPropertiesDto { Role = "Table", ReadingOrder = 1 }
                        },
                        new ElementDto
                        {
                            Id = "help",
                            Type = "link",
                            X = 20, Y = 130, Width = 120, Height = 24,
                            Content = "Open help",
                            Href = "https://example.test/help",
                            Accessibility = new AccessibilityPropertiesDto { Role = "Link", ReadingOrder = 2 }
                        },
                        new ElementDto
                        {
                            Id = "customer-name",
                            Type = "field",
                            X = 20, Y = 180, Width = 180, Height = 42,
                            FieldLabel = "Customer name",
                            Accessibility = new AccessibilityPropertiesDto { Role = "Form", ReadingOrder = 3 }
                        }
                    ]
                }
            ]
        };

        var document = DesignJsonMapper.MapToPdfDocument(
            design,
            new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")));
        var pdf = System.Text.Encoding.Latin1.GetString(
            document.ToBytes(DesignJsonMapper.BuildSaveOptions(design)));

        Assert.Contains("/Type /StructElem /S /TR", pdf);
        Assert.Contains("/Type /StructElem /S /TH", pdf);
        Assert.Contains("/Type /StructElem /S /TD", pdf);
        Assert.Contains("/Type /StructElem /S /Link", pdf);
        Assert.Contains("/Type /StructElem /S /Form", pdf);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(pdf, "/Type /OBJR").Count);
        Assert.Contains("/Subtype /Link", pdf);
        Assert.Contains("/Subtype /Widget", pdf);
        Assert.Contains("/Tabs /S", pdf);
    }

    [Fact]
    public void BuildPdfSaveOptions_MapsPdfA3bComplianceProfile()
    {
        var design = new DesignExportDto
        {
            PageSettings = new PageSettingsDto { ComplianceProfile = "PdfA3B" }
        };

        var options = DesignJsonMapper.BuildSaveOptions(design);

        Assert.NotNull(options);
        Assert.Equal(PdfComplianceProfile.PdfA3B, options.ComplianceProfile);
    }

    [Fact]
    public void BuildSaveOptions_MapsPdfA2bProfile()
    {
        var design = new DesignExportDto
        {
            PageSettings = new PageSettingsDto { ComplianceProfile = "PdfA2B" }
        };

        var options = DesignJsonMapper.BuildSaveOptions(design);

        Assert.NotNull(options);
        Assert.Equal(PdfComplianceProfile.PdfA2B, options.ComplianceProfile);
        Assert.Null(options.Encryption);
    }

    [Fact]
    public void BuildSaveOptions_RejectsPdfAWithEncryption()
    {
        var design = new DesignExportDto
        {
            PageSettings = new PageSettingsDto
            {
                ComplianceProfile = "PdfA2B",
                Encryption = new PdfEncryptionDto
                {
                    Enabled = true,
                    Algorithm = "Aes256",
                    UserPassword = "reader"
                }
            }
        };

        var exception = Assert.Throws<InvalidDataException>(() => DesignJsonMapper.BuildSaveOptions(design));

        Assert.Contains("cannot be enabled together", exception.Message);
    }
}
