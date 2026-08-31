using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Trust;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfAProfileRestrictionTests
{
    public static TheoryData<string> ForbiddenActionTypes => new()
    {
        "JavaScript", "Launch", "Sound", "Movie", "ResetForm", "ImportData", "Hide",
        "Rendition", "Trans", "SetOCGState", "GoTo3DView", "SetState", "NoOp"
    };

    [Theory]
    [MemberData(nameof(ForbiddenActionTypes))]
    public async Task Preflight_ShouldBlockForbiddenActionTypes(string actionType)
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("Action"),
            ["S"] = new PdfName(actionType)
        });

        var report = await Preflight(source);

        Assert.Equal(PdfComplianceConversionCapability.Unsupported, report.Capability);
        Assert.Contains(report.Findings, item => item.Code == "PXAPDFTRUST325" && item.Message.Contains(actionType, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("URI")]
    [InlineData("GoTo")]
    [InlineData("GoToR")]
    public async Task Preflight_ShouldNotBlockPermittedActionTypes(string actionType)
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("Action"),
            ["S"] = new PdfName(actionType)
        });

        var report = await Preflight(source);

        Assert.DoesNotContain(report.Findings, item => item.Code is "PXAPDFTRUST315" or "PXAPDFTRUST325");
    }

    [Fact]
    public async Task Preflight_ShouldBlockAdditionalActions()
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["AA"] = new PdfDictionary
            {
                ["E"] = new PdfDictionary { ["S"] = new PdfName("URI") }
            }
        });

        var report = await Preflight(source);

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST324");
    }

    [Theory]
    [InlineData("RichMedia")]
    [InlineData("Movie")]
    [InlineData("Sound")]
    [InlineData("Screen")]
    [InlineData("3D")]
    public async Task Preflight_ShouldBlockUnsupportedAnnotationSubtypes(string subtype)
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName(subtype),
            ["F"] = new PdfInteger(4)
        });

        var report = await Preflight(source);

        Assert.Contains(report.Findings, item => item.Code == "PXAPDFTRUST316" && item.Message.Contains(subtype, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PdfA3Preflight_ShouldBlockFileAttachmentAnnotationsWithoutAnEmbeddedFileSpecification()
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("FileAttachment"),
            ["F"] = new PdfInteger(4)
        });

        var report = await new PdfA2bConverter(new PdfTrustInspector()).PreflightAsync(
            new MemoryStream(source, writable: false),
            new PdfA2bConversionOptions { Profile = PdfArchiveProfile.PdfA3b });

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST331");
    }

    [Fact]
    public async Task Preflight_ShouldBlockHiddenOrNonPrintingAnnotations()
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Text"),
            ["F"] = new PdfInteger(2)
        });

        var report = await Preflight(source);

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST328");
    }

    [Fact]
    public async Task Preflight_ShouldBlockAnnotationsWithoutPrintFlags()
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Text")
        });

        var report = await Preflight(source);

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST328");
    }

    [Fact]
    public async Task Preflight_ShouldInspectActionsNestedInsideArrays()
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Kids"] = new PdfArray([
                new PdfArray([
                    new PdfDictionary
                    {
                        ["Type"] = new PdfName("Action"),
                        ["S"] = new PdfName("Launch")
                    }
                ])
            ])
        });

        var report = await Preflight(source);

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST325");
    }

    [Fact]
    public async Task Preflight_ShouldBlockExternalStreamDependencies()
    {
        var source = AppendObject(CreateBaseDocument(), new PdfStreamObject(
            new PdfDictionary { ["F"] = new PdfString("external.bin"u8.ToArray(), IsHex: false) },
            Array.Empty<byte>()));

        var report = await Preflight(source);

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST327");
    }

    [Fact]
    public async Task Inspector_ShouldRejectForbiddenActionInClaimedPdfA()
    {
        var source = AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("Action"),
            ["S"] = new PdfName("JavaScript"),
            ["JS"] = new PdfString("app.alert('no')"u8.ToArray(), IsHex: false)
        });

        var report = await new PdfTrustInspector().InspectAsync(new MemoryStream(source, writable: false));

        Assert.Equal(PdfTrustStatus.Invalid, report.PdfA.Status);
        Assert.Contains(report.Diagnostics, static item => item.Code == "PXAPDFTRUST315");
        Assert.Contains(report.Diagnostics, static item => item.Code == "PXAPDFTRUST325");
    }

    private static Task<PdfA2bConversionPreflightReport> Preflight(byte[] source) =>
        new PdfA2bConverter(new PdfTrustInspector()).PreflightAsync(new MemoryStream(source, writable: false));

    private static byte[] CreateBaseDocument()
    {
        var document = new PdfDocument
        {
            FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"))
        };
        document.AddPage().DrawText("PDF/A restrictions", 40, 40, new PdfDrawTextOptions { Language = "en" });
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA2B });
    }

    private static byte[] AppendObject(byte[] source, PdfObject value)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var id = new PdfObjectId(table.Entries.Keys.Max(static item => item.Number) + 1, 0);
        return new PdfIncrementalUpdateWriter().Append(source, [new PdfIncrementalObjectUpdate(id, value)]).Bytes;
    }
}
