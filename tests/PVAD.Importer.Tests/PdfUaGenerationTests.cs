using System.Text;
using System.Text.RegularExpressions;
using PVAD.Pdf;
using PVAD.Importer.Trust;

namespace PVAD.Importer.Tests;

public sealed class PdfUaGenerationTests
{
    [Fact]
    public async Task PdfUa1_WritesStructureTreeParentTreeMarkedContentAndMetadata()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        page.DrawRectangle(20, 700, 100, 20);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.H1,
            ElementId = "heading",
            ReadingOrder = 2,
            ActualText = "Accessible heading",
            Language = "en-US"
        });
        page.DrawRectangle(20, 650, 100, 20);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.P,
            ElementId = "intro",
            ReadingOrder = 1
        });
        page.DrawLine(20, 640, 120, 640);
        page.MarkLastElementAsArtifact();

        var bytes = document.ToBytes(new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        WriteValidationArtifact("pxa-pdfua1-structure.pdf", bytes);
        var pdf = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.7", pdf);
        Assert.Contains("/Type /StructTreeRoot", pdf);
        Assert.Contains("/Type /StructElem /S /Document", pdf);
        Assert.Contains("/ParentTree", pdf);
        Assert.Contains("/StructParents 0", pdf);
        Assert.Contains("/MarkInfo << /Marked true >>", pdf);
        Assert.Contains("/H1 <</MCID 0>> BDC", pdf);
        Assert.Contains("/P <</MCID 1>> BDC", pdf);
        Assert.Contains("/Artifact BMC", pdf);
        Assert.Contains("<pdfuaid:part>1</pdfuaid:part>", pdf);
        Assert.Contains("/ViewerPreferences << /DisplayDocTitle true >>", pdf);
        Assert.Contains("/Lang <FEFF0065006E002D00550053>", pdf);

        var headingObject = FindStructureObjectId(pdf, "heading");
        var introObject = FindStructureObjectId(pdf, "intro");
        var documentStructureMatch = Regex.Match(pdf,
            @"(?<id>\d+) 0 obj\s*<< /Type /StructElem /S /Document.*?/K \[(?<kids>[^]]*)\]",
            RegexOptions.Singleline);
        Assert.True(documentStructureMatch.Success);
        Assert.Equal($"{introObject} 0 R {headingObject} 0 R", documentStructureMatch.Groups["kids"].Value);
        Assert.Matches($@"/Type /StructTreeRoot /K \[{documentStructureMatch.Groups["id"].Value} 0 R\]", pdf);

        await using var stream = new MemoryStream(bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        Assert.True(report.PdfUa.IsClaimed);
        Assert.Equal("PDF/UA-1", report.PdfUa.Profile);
    }

    [Fact]
    public void PdfUa1_PreflightFailsClosedForMissingDocumentSemanticsAndUntaggedContent()
    {
        var document = new PdfDocument();
        document.AddPage().DrawRectangle(10, 10, 20, 20);

        var exception = Assert.Throws<PdfAccessibilityException>(() => document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 }));

        Assert.Contains(exception.Report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA002");
        Assert.Contains(exception.Report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA003");
        Assert.Contains(exception.Report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA005");
    }

    [Fact]
    public void PdfUa1_PreflightRejectsInvalidDocumentLanguageTag()
    {
        var document = CreateDocument();
        document.Language = "not a valid language";
        var page = document.AddPage();
        page.DrawRectangle(10, 10, 20, 20);
        page.TagLastElement(new PdfAccessibilityProperties { Role = PdfStructureRole.P, ElementId = "paragraph" });

        var report = PdfAccessibilityPreflight.Validate(document,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });

        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA015");
    }

    [Fact]
    public void PdfUa1_WritesProviderSpecificRoleThroughRoleMap()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        page.DrawRectangle(10, 10, 20, 20);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.P,
            CustomRole = "NutrientCallout",
            NamespaceUri = "urn:powerdox:migration:nutrient",
            ElementId = "provider-callout"
        });

        var bytes = document.ToBytes(new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        WriteValidationArtifact("pxa-pdfua1-custom-role.pdf", bytes);
        var pdf = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/NutrientCallout <</MCID 0>> BDC", pdf);
        Assert.Contains("/Type /StructElem /S /NutrientCallout", pdf);
        Assert.Contains("/RoleMap << /NutrientCallout /P >>", pdf);
        Assert.DoesNotContain("/Namespaces", pdf);
        Assert.DoesNotContain("/NS ", pdf);
    }

    [Fact]
    public void PdfUa1_PreflightRejectsInvalidAndConflictingCustomRoles()
    {
        var invalidDocument = CreateDocument();
        var invalidPage = invalidDocument.AddPage();
        invalidPage.DrawRectangle(10, 10, 20, 20);
        invalidPage.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.P,
            CustomRole = "Invalid role",
            NamespaceUri = "relative/provider",
            ElementId = "invalid"
        });

        var invalidReport = PdfAccessibilityPreflight.Validate(invalidDocument,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        Assert.Contains(invalidReport.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA025");
        Assert.Contains(invalidReport.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA027");

        var conflictingDocument = CreateDocument();
        var conflictingPage = conflictingDocument.AddPage();
        conflictingPage.DrawRectangle(10, 10, 20, 20);
        conflictingPage.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.P,
            CustomRole = "ProviderBlock",
            NamespaceUri = "urn:provider:one",
            ElementId = "first"
        });
        conflictingPage.DrawRectangle(40, 10, 20, 20);
        conflictingPage.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Div,
            CustomRole = "ProviderBlock",
            NamespaceUri = "urn:provider:two",
            ElementId = "second"
        });

        var conflictingReport = PdfAccessibilityPreflight.Validate(conflictingDocument,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        Assert.Contains(conflictingReport.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA026");
        Assert.Contains(conflictingReport.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA028");
    }

    [Fact]
    public void PdfUa1_PreflightRequiresFigureAlternativesAndTableHeaderScope()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        page.DrawRectangle(10, 10, 20, 20);
        page.TagLastElement(new PdfAccessibilityProperties { Role = PdfStructureRole.Figure, ElementId = "figure" });
        page.DrawRectangle(40, 10, 20, 20);
        page.TagLastElement(new PdfAccessibilityProperties { Role = PdfStructureRole.TH, ElementId = "header" });

        var report = PdfAccessibilityPreflight.Validate(document,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });

        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA007" && diagnostic.ElementId == "figure");
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA009" && diagnostic.ElementId == "header");
    }

    [Fact]
    public void PdfUa1_WritesTableSemanticsAndHeaderAssociations()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        page.DrawRectangle(5, 5, 50, 50);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Table,
            ElementId = "table"
        });
        page.DrawRectangle(10, 30, 40, 20);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.TR,
            ElementId = "row-1",
            ParentElementId = "table"
        });
        page.DrawRectangle(10, 30, 40, 20);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.TH,
            ElementId = "column-name",
            ParentElementId = "row-1",
            TableHeaderScope = PdfTableHeaderScope.Column
        });
        page.DrawRectangle(10, 10, 40, 20);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.TR,
            ElementId = "row-2",
            ParentElementId = "table"
        });
        page.DrawRectangle(10, 10, 40, 20);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.TD,
            ElementId = "cell-name",
            ParentElementId = "row-2",
            TableHeaderIds = ["column-name"]
        });

        var bytes = document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        WriteValidationArtifact("pxa-pdfua1-table.pdf", bytes);
        var pdf = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/A << /O /Table /Scope /Column >>", pdf);
        Assert.Contains("/A << /O /Table /Headers [<FEFF0063006F006C0075006D006E002D006E0061006D0065>] >>", pdf);
        var tableObject = FindStructureObjectId(pdf, "table");
        var headerRowObject = FindStructureObjectId(pdf, "row-1");
        var headerObject = FindStructureObjectId(pdf, "column-name");
        var headerStart = pdf.IndexOf($"{headerObject} 0 obj", StringComparison.Ordinal);
        var headerEnd = pdf.IndexOf("endobj", headerStart, StringComparison.Ordinal);
        Assert.Contains($"/P {headerRowObject} 0 R", pdf[headerStart..headerEnd]);
        var tableStart = pdf.IndexOf($"{tableObject} 0 obj", StringComparison.Ordinal);
        var tableEnd = pdf.IndexOf("endobj", tableStart, StringComparison.Ordinal);
        Assert.Contains($"{headerRowObject} 0 R", pdf[tableStart..tableEnd]);
    }

    [Fact]
    public void PdfUa1_DrawSimpleTableCreatesRowsCellsAndColumnHeaderAssociations()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Table,
            ElementId = "orders"
        }))
        {
            page.DrawSimpleTable(20, 700, 240,
            [
                ["", ""],
                ["", ""]
            ], new PdfTableOptions { HasHeaderRow = true });
        }

        var pdf = Encoding.Latin1.GetString(document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 }));

        Assert.Equal(2, Regex.Matches(pdf, "/Type /StructElem /S /TR").Count);
        Assert.Equal(2, Regex.Matches(pdf, "/Type /StructElem /S /TH").Count);
        Assert.Equal(2, Regex.Matches(pdf, "/Type /StructElem /S /TD").Count);
        Assert.Contains("/Scope /Column", pdf);
        Assert.Contains("/Headers [<FEFF006F00720064006500720073002D00720031002D00630031>]", pdf);
        Assert.Contains("/Artifact BMC", pdf);
    }

    [Fact]
    public void PdfUa1_AssociatesLinkAnnotationWithLinkStructureElement()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Link,
            ElementId = "help-link",
            ActualText = "Open help"
        }))
        {
            page.DrawRectangle(20, 700, 100, 20);
            page.AddWebLink(20, 700, 100, 20, "https://example.test/help");
        }

        var pdf = Encoding.Latin1.GetString(document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 }));

        Assert.Contains("/Subtype /Link", pdf);
        Assert.Contains("/StructParent 1", pdf);
        Assert.Contains("/Type /OBJR", pdf);
        Assert.Contains("/ParentTreeNextKey 2", pdf);
        Assert.Contains("/Tabs /S", pdf);
        Assert.Contains("1 ", pdf[pdf.IndexOf("/Nums [", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void PdfUa1_AssociatesWidgetWithFormStructureAndTooltip()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "customer-name",
            AlternateText = "Customer name"
        }))
        {
            page.AddTextField("customerName", 20, 700, 180, 24);
        }

        var pdf = Encoding.Latin1.GetString(document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 }));

        Assert.Contains("/Subtype /Widget", pdf);
        Assert.Contains("/StructParent 1", pdf);
        Assert.Contains("/TU <FEFF0043007500730074006F006D006500720020006E0061006D0065>", pdf);
        Assert.Contains("/Type /StructElem /S /Form", pdf);
        Assert.Contains("/Type /OBJR", pdf);
    }

    [Fact]
    public void PdfUa1_WritesNativeRadioListSignatureAndPushButtonWidgets()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "choice",
            AlternateText = "Preferred option",
            ReadingOrder = 1
        }))
        {
            page.AddRadioButton("preference", "Yes", 0, 20, 700, 14, 14, true);
            page.AddRadioButton("preference", "No", 1, 20, 680, 14, 14);
        }
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "topics",
            AlternateText = "Topics",
            ReadingOrder = 2
        }))
            page.AddListBox("topics", 60, 650, 120, 50, ["PDF", "Spreadsheet"], ["PDF"], multiSelect: true);
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "approval-signature",
            AlternateText = "Approval signature",
            ReadingOrder = 3
        }))
            page.AddSignatureField("approval", 60, 590, 120, 40);
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "reset",
            AlternateText = "Reset form",
            ReadingOrder = 4
        }))
            page.AddPushButton("resetButton", "Reset", 60, 550, 80, 24, "reset");

        var bytes = document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        WriteValidationArtifact("pxa-pdfua1-widgets.pdf", bytes);
        var pdf = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/FT /Btn /Ff 32768", pdf);
        Assert.Contains("/O /PrintField /Role /rb", pdf);
        Assert.Contains("/O /PrintField /Role /tv", pdf);
        Assert.Contains("/O /PrintField /Role /pb", pdf);
        Assert.Contains("/Kids [", pdf);
        Assert.Contains("/AS /R1", pdf);
        Assert.Contains("/FT /Ch /Ff 2097152", pdf);
        Assert.Contains("/FT /Sig", pdf);
        Assert.Contains("/FT /Btn /Ff 65536", pdf);
        Assert.Contains("/S /ResetForm", pdf);
        Assert.Equal(5, Regex.Matches(pdf, "/Type /OBJR").Count);
    }

    [Fact]
    public void PdfUa1_PreflightReportsInvalidRadioGroupsAndAmbiguousFocusOrder()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "invalid-radio",
            AlternateText = "Invalid choice",
            ReadingOrder = 1
        }))
            page.AddRadioButton("invalid", "Only", 0, 20, 700, 14, 14, true);
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "duplicate-order",
            AlternateText = "Name",
            ReadingOrder = 1
        }))
            page.AddTextField("name", 50, 700, 100, 20);
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Link,
            ElementId = "implicit-order",
            ActualText = "Help"
        }))
            page.AddWebLink(50, 650, 100, 20, "https://example.test");

        var report = PdfAccessibilityPreflight.Validate(document,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });

        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA022");
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA023"
            && diagnostic.Severity == PdfAccessibilityDiagnosticSeverity.Warning);
        Assert.Equal(2, report.Diagnostics.Count(diagnostic => diagnostic.Code == "PXAPDFUA024"));
        Assert.All(report.Diagnostics, diagnostic =>
        {
            Assert.Equal($"diagnostics.{diagnostic.Code}", diagnostic.LocalizationKey);
            Assert.Equal(diagnostic.Code, diagnostic.LocalizationParameters!["code"]);
        });
    }

    [Fact]
    public void PdfUa1_OrdersPageAnnotationsByExplicitReadingOrder()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "last-field",
            AlternateText = "Last field",
            ReadingOrder = 20
        }))
            page.AddTextField("last", 20, 700, 100, 20);
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Form,
            ElementId = "first-field",
            AlternateText = "First field",
            ReadingOrder = 10
        }))
            page.AddTextField("first", 20, 650, 100, 20);

        var pdf = Encoding.Latin1.GetString(document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 }));
        var lastId = FindAnnotationObjectId(pdf, "Last field");
        var firstId = FindAnnotationObjectId(pdf, "First field");
        var annotations = Regex.Match(pdf, @"/Annots \[(?<ids>[^]]+)\]").Groups["ids"].Value;

        Assert.True(annotations.IndexOf($"{firstId} 0 R", StringComparison.Ordinal)
            < annotations.IndexOf($"{lastId} 0 R", StringComparison.Ordinal));
    }

    [Fact]
    public void PdfUa1_PreflightRejectsUnstructuredAnnotationsAndIncompleteTables()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        page.AddWebLink(20, 700, 100, 20, "https://example.test");
        page.DrawRectangle(20, 600, 100, 40);
        page.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Table,
            ElementId = "incomplete-table"
        });

        var report = PdfAccessibilityPreflight.Validate(document,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });

        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA010");
        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Code == "PXAPDFUA020" && diagnostic.ElementId == "incomplete-table");
    }

    [Fact]
    public void PdfUa1_SemanticScopeGroupsMultipleDrawingOperations()
    {
        var document = CreateDocument();
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Figure,
            ElementId = "chart",
            AlternateText = "Revenue chart"
        }))
        {
            page.DrawRectangle(10, 10, 100, 60);
            page.DrawLine(10, 10, 110, 70);
        }

        var pdf = Encoding.Latin1.GetString(document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 }));

        Assert.Contains("/Figure <</MCID 0>> BDC", pdf);
        Assert.Contains("/Figure <</MCID 1>> BDC", pdf);
        Assert.Contains("/K [<< /Type /MCR", pdf);
        Assert.Single(Regex.Matches(pdf, "/Type /StructElem /S /Figure").Cast<Match>());
    }

    [Fact]
    public void PdfUa1_PreflightRejectsMissingAndCyclicSemanticParents()
    {
        var missingParentDocument = CreateDocument();
        var missingParentPage = missingParentDocument.AddPage();
        missingParentPage.DrawRectangle(10, 10, 20, 20);
        missingParentPage.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.P,
            ElementId = "orphan",
            ParentElementId = "missing"
        });
        var missingReport = PdfAccessibilityPreflight.Validate(missingParentDocument,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        Assert.Contains(missingReport.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA013");

        var cyclicDocument = CreateDocument();
        var cyclicPage = cyclicDocument.AddPage();
        cyclicPage.DrawRectangle(10, 10, 20, 20);
        cyclicPage.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Div,
            ElementId = "a",
            ParentElementId = "b"
        });
        cyclicPage.DrawRectangle(40, 10, 20, 20);
        cyclicPage.TagLastElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.Div,
            ElementId = "b",
            ParentElementId = "a"
        });
        var cyclicReport = PdfAccessibilityPreflight.Validate(cyclicDocument,
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
        Assert.Contains(cyclicReport.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFUA014");
    }

    private static PdfDocument CreateDocument()
    {
        var document = new PdfDocument { Language = "en-US" };
        document.Info.Title = "Accessible document";
        return document;
    }

    private static void WriteValidationArtifact(string fileName, byte[] bytes)
    {
        if (PVAD.Testing.PvadTestEnvironment.Read("PVAD_PDFUA_ARTIFACT_DIR") is not { Length: > 0 } directory)
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
    }

    private static int FindStructureObjectId(string pdf, string elementId)
    {
        var encodedId = $"FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(elementId))}";
        var match = Regex.Match(pdf, $@"(?m)^(\d+) 0 obj\n<< /Type /StructElem .*?/ID <{encodedId}>");
        Assert.True(match.Success, $"Structure element '{elementId}' was not found.");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int FindAnnotationObjectId(string pdf, string tooltip)
    {
        var encoded = $"FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(tooltip))}";
        var match = Regex.Match(pdf, $@"(?m)^(\d+) 0 obj\n<< /Type /Annot .*?/TU <{encoded}>");
        Assert.True(match.Success, $"Annotation '{tooltip}' was not found.");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
