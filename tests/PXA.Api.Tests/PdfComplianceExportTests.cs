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
            SharedElements =
            [
                new ElementDto
                {
                    Id = "shared-header",
                    Type = "text",
                    X = 20, Y = 8, Width = 200, Height = 16,
                    Content = "Repeated document header",
                    Accessibility = new AccessibilityPropertiesDto { Role = "P" }
                },
                new ElementDto
                {
                    Id = "shared-footer",
                    Type = "pagenumber",
                    X = 260, Y = 810, Width = 80, Height = 16,
                    NumberingFormat = "pageOfTotal",
                    Accessibility = new AccessibilityPropertiesDto { Role = "P" }
                }
            ],
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
                            Accessibility = new AccessibilityPropertiesDto
                            {
                                Role = "Table",
                                CustomRole = "ProviderOrdersTable",
                                NamespaceUri = "urn:powerdox:migration:provider",
                                ReadingOrder = 1
                            }
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
                        },
                        new ElementDto
                        {
                            Id = "delivery-choice",
                            Type = "radio",
                            X = 20, Y = 240, Width = 160, Height = 50,
                            FieldLabel = "Delivery method",
                            Options = ["Email", "Post"],
                            SelectedValue = "Email",
                            Accessibility = new AccessibilityPropertiesDto { Role = "Form", ReadingOrder = 4 }
                        },
                        new ElementDto
                        {
                            Id = "topics",
                            Type = "optionlist",
                            X = 220, Y = 240, Width = 140, Height = 50,
                            FieldLabel = "Topics",
                            Options = ["PDF", "Spreadsheet"],
                            SelectedValue = "PDF",
                            MultiSelect = true,
                            Accessibility = new AccessibilityPropertiesDto { Role = "Form", ReadingOrder = 5 }
                        },
                        new ElementDto
                        {
                            Id = "approval",
                            Type = "signature",
                            X = 20, Y = 320, Width = 180, Height = 50,
                            SignatureLabel = "Approval signature",
                            Accessibility = new AccessibilityPropertiesDto { Role = "Form", ReadingOrder = 6 }
                        },
                        new ElementDto
                        {
                            Id = "reset",
                            Type = "button",
                            X = 220, Y = 320, Width = 100, Height = 30,
                            Content = "Reset",
                            ButtonAction = "reset",
                            Accessibility = new AccessibilityPropertiesDto { Role = "Form", ReadingOrder = 7 }
                        }
                    ]
                }
            ]
        };

        var document = DesignJsonMapper.MapToPdfDocument(
            design,
            new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")));
        var bytes = document.ToBytes(DesignJsonMapper.BuildSaveOptions(design));
        WriteValidationArtifact("pxa-designer-pdfua1-elements.pdf", bytes);
        var pdf = System.Text.Encoding.Latin1.GetString(bytes);

        Assert.Contains("/Type /StructElem /S /TR", pdf);
        Assert.Contains("/Type /StructElem /S /TH", pdf);
        Assert.Contains("/Type /StructElem /S /TD", pdf);
        Assert.Contains("/Type /StructElem /S /ProviderOrdersTable", pdf);
        Assert.Contains("/RoleMap << /ProviderOrdersTable /Table >>", pdf);
        Assert.Contains("/Type /StructElem /S /Link", pdf);
        Assert.Contains("/Type /StructElem /S /Form", pdf);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(pdf, "/Type /OBJR").Count);
        Assert.Contains("/Subtype /Link", pdf);
        Assert.Contains("/Subtype /Widget", pdf);
        Assert.Contains("/FT /Btn /Ff 32768", pdf);
        Assert.Contains("/FT /Ch /Ff 2097152", pdf);
        Assert.Contains("/FT /Sig", pdf);
        Assert.Contains("/FT /Btn /Ff 65536", pdf);
        Assert.Contains("/S /ResetForm", pdf);
        Assert.Contains("/Tabs /S", pdf);
        Assert.True(System.Text.RegularExpressions.Regex.Matches(pdf, "/Artifact BMC").Count >= 2);
    }

    [Fact]
    public void DesignerModel_KeepsStaticCheckMarksAsArtifactsInsteadOfFormWidgets()
    {
        var design = new DesignExportDto
        {
            Name = "Accessible static mark",
            PageSettings = new PageSettingsDto
            {
                SystemLanguage = "en-US",
                AccessibilityProfile = "PdfUa1",
                Metadata = new PdfMetadataDto { Title = "Accessible static mark" }
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
                            Id = "static-mark",
                            Type = "checkmark",
                            X = 20, Y = 20, Width = 40, Height = 40,
                            CheckState = "empty",
                            Accessibility = new AccessibilityPropertiesDto
                            {
                                Role = "Span",
                                IsArtifact = true
                            }
                        }
                    ]
                }
            ]
        };

        var document = DesignJsonMapper.MapToPdfDocument(design);
        var pdf = System.Text.Encoding.Latin1.GetString(
            document.ToBytes(DesignJsonMapper.BuildSaveOptions(design)));

        Assert.Contains("/Artifact BMC", pdf);
        Assert.DoesNotContain("/Subtype /Widget", pdf);
        Assert.DoesNotContain("/AcroForm", pdf);
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

    private static void WriteValidationArtifact(string fileName, byte[] bytes)
    {
        if (Environment.GetEnvironmentVariable("PXA_PDFUA_ARTIFACT_DIR") is not { Length: > 0 } directory)
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
    }
}
