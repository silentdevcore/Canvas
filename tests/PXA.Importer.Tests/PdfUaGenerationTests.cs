using System.Text;
using System.Text.RegularExpressions;
using PXA.Pdf;
using PXA.Importer.Trust;

namespace PXA.Importer.Tests;

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

        var pdf = Encoding.Latin1.GetString(document.ToBytes(
            new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 }));

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

    private static int FindStructureObjectId(string pdf, string elementId)
    {
        var encodedId = $"FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(elementId))}";
        var match = Regex.Match(pdf, $@"(?m)^(\d+) 0 obj\n<< /Type /StructElem .*?/ID <{encodedId}>");
        Assert.True(match.Success, $"Structure element '{elementId}' was not found.");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
