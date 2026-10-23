using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;
using PVAD.Pdf;

namespace PVAD.Importer.Tests;

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
    public async Task Preflight_ShouldRequireANormalAnnotationAppearance()
    {
        var report = await Preflight(AppendObject(CreateBaseDocument(), Annotation("Text")));

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST333");
    }

    [Theory]
    [InlineData("Popup", false)]
    [InlineData("Link", true)]
    public async Task Preflight_ShouldAllowAppearanceExceptions(string subtype, bool includeFlags)
    {
        var annotation = Annotation(subtype);
        if (!includeFlags)
        {
            annotation["F"] = null;
        }

        var report = await Preflight(AppendObject(CreateBaseDocument(), annotation));

        Assert.DoesNotContain(report.Findings, static item => item.Code is "PXAPDFTRUST328" or "PXAPDFTRUST333" or "PXAPDFTRUST334");
    }

    [Fact]
    public async Task Preflight_ShouldRejectNonNormalAppearanceEntries()
    {
        var annotation = Annotation("Text");
        annotation["AP"] = new PdfDictionary
        {
            ["N"] = AppearanceStream(),
            ["R"] = AppearanceStream()
        };

        var report = await Preflight(AppendObject(CreateBaseDocument(), annotation));

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST334");
    }

    [Fact]
    public async Task Preflight_ShouldRequireButtonAppearanceStates()
    {
        var annotation = Annotation("Widget");
        annotation["FT"] = new PdfName("Btn");
        annotation["AP"] = new PdfDictionary { ["N"] = AppearanceStream() };

        var report = await Preflight(AppendObject(CreateBaseDocument(), annotation));

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST334");
    }

    [Fact]
    public async Task Preflight_ShouldAcceptACompleteButtonAppearanceStateDictionary()
    {
        var annotation = Annotation("Widget");
        annotation["FT"] = new PdfName("Btn");
        annotation["AP"] = new PdfDictionary
        {
            ["N"] = new PdfDictionary
            {
                ["Off"] = AppearanceStream(),
                ["On"] = AppearanceStream()
            }
        };

        var report = await Preflight(AppendObject(CreateBaseDocument(), annotation));

        Assert.DoesNotContain(report.Findings, static item => item.Code is "PXAPDFTRUST333" or "PXAPDFTRUST334");
    }

    [Fact]
    public async Task Preflight_ShouldRejectUnknownAnnotationSubtypesAndToggleNoView()
    {
        var unknown = Annotation("FutureAnnotation");
        var toggleNoView = Annotation("Text");
        toggleNoView["F"] = new PdfInteger(4 | 256);
        toggleNoView["AP"] = new PdfDictionary { ["N"] = AppearanceStream() };

        var report = await Preflight(AppendObjects(CreateBaseDocument(), unknown, toggleNoView));

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST316");
        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST328");
    }

    [Fact]
    public async Task Preflight_ShouldRejectDynamicFormAppearanceAndXfaFlags()
    {
        var source = UpdateCatalog(CreateBaseDocument(), catalog =>
        {
            catalog["NeedsRendering"] = new PdfBoolean(true);
            catalog["AcroForm"] = new PdfDictionary
            {
                ["NeedAppearances"] = new PdfBoolean(true),
                ["XFA"] = new PdfString("dynamic"u8.ToArray(), IsHex: false)
            };
        });

        var report = await Preflight(source);

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST335");
    }

    [Fact]
    public async Task Preflight_ShouldRejectWidgetActions()
    {
        var annotation = Annotation("Widget");
        annotation["FT"] = new PdfName("Tx");
        annotation["A"] = new PdfDictionary { ["S"] = new PdfName("URI") };
        annotation["AP"] = new PdfDictionary { ["N"] = AppearanceStream() };

        var report = await Preflight(AppendObject(CreateBaseDocument(), annotation));

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST335");
    }

    [Theory]
    [InlineData("CustomIntent", "Normal", "PXAPDFTRUST340")]
    [InlineData("RelativeColorimetric", "CustomBlend", "PXAPDFTRUST341")]
    public async Task Preflight_ShouldRejectUnknownRenderingIntentsAndBlendModes(
        string renderingIntent,
        string blendMode,
        string expectedCode)
    {
        var report = await Preflight(AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("ExtGState"),
            ["RI"] = new PdfName(renderingIntent),
            ["BM"] = new PdfName(blendMode)
        }));

        Assert.Contains(report.Findings, item => item.Code == expectedCode);
    }

    [Fact]
    public async Task Preflight_ShouldInspectRenderingIntentOperatorsInContentStreams()
    {
        var form = new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("XObject"),
                ["Subtype"] = new PdfName("Form"),
                ["Length"] = new PdfInteger(16)
            },
            "/CustomIntent ri"u8.ToArray());

        var report = await Preflight(AppendObject(CreateBaseDocument(), form));

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST340");
    }

    [Fact]
    public async Task Preflight_ShouldAcceptStandardRenderingIntentsAndBlendModes()
    {
        var report = await Preflight(AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("ExtGState"),
            ["RI"] = new PdfName("RelativeColorimetric"),
            ["BM"] = new PdfArray([
                new PdfName("Normal"),
                new PdfName("Multiply")
            ])
        }));

        Assert.DoesNotContain(report.Findings, static item => item.Code is "PXAPDFTRUST340" or "PXAPDFTRUST341");
    }

    [Fact]
    public async Task Preflight_ShouldPreserveAValidSoftMaskInsteadOfBlockingTransparency()
    {
        var report = await Preflight(AppendObject(CreateBaseDocument(), new PdfDictionary
        {
            ["Type"] = new PdfName("ExtGState"),
            ["BM"] = new PdfName("Normal"),
            ["SMask"] = new PdfDictionary
            {
                ["S"] = new PdfName("Luminosity"),
                ["G"] = AppearanceStream()
            }
        }));

        Assert.DoesNotContain(report.Findings, static item => item.Code is
            "PXAPDFTRUST340" or "PXAPDFTRUST341" or "PXAPDFTRUST343");
    }

    [Fact]
    public async Task Preflight_ShouldValidateOptionalContentConfigurations()
    {
        var source = UpdateCatalog(CreateBaseDocument(), catalog =>
        {
            catalog["OCProperties"] = new PdfDictionary
            {
                ["OCGs"] = new PdfArray([new PdfReference(new PdfObjectId(900, 0))]),
                ["D"] = new PdfDictionary
                {
                    ["Name"] = new PdfString("Default"u8.ToArray(), IsHex: false),
                    ["Order"] = new PdfArray()
                },
                ["Configs"] = new PdfArray([
                    new PdfDictionary
                    {
                        ["Name"] = new PdfString("Default"u8.ToArray(), IsHex: false),
                        ["AS"] = new PdfArray()
                    }
                ])
            };
        });

        var report = await Preflight(source);

        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST337");
        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST338");
        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST339");
    }

    [Fact]
    public async Task Preflight_ShouldAcceptACompleteOptionalContentConfiguration()
    {
        var source = UpdateCatalog(CreateBaseDocument(), catalog =>
        {
            catalog["OCProperties"] = new PdfDictionary
            {
                ["OCGs"] = new PdfArray(),
                ["D"] = new PdfDictionary
                {
                    ["Name"] = new PdfString("Default"u8.ToArray(), IsHex: false),
                    ["Order"] = new PdfArray()
                },
                ["Configs"] = new PdfArray([
                    new PdfDictionary
                    {
                        ["Name"] = new PdfString("Print"u8.ToArray(), IsHex: false)
                    }
                ])
            };
        });

        var report = await Preflight(source);

        Assert.DoesNotContain(report.Findings, static item => item.Code is
            "PXAPDFTRUST336" or "PXAPDFTRUST337" or "PXAPDFTRUST338" or "PXAPDFTRUST339");
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
        => AppendObjects(source, value);

    private static byte[] AppendObjects(byte[] source, params PdfObject[] values)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var nextId = table.Entries.Keys.Max(static item => item.Number) + 1;
        var updates = values.Select((value, index) =>
            new PdfIncrementalObjectUpdate(new PdfObjectId(nextId + index, 0), value));
        return new PdfIncrementalUpdateWriter().Append(source, updates).Bytes;
    }

    private static byte[] UpdateCatalog(byte[] source, Action<PdfDictionary> update)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var rootReference = Assert.IsType<PdfReference>(graph.Trailer!["Root"]);
        var catalog = Assert.IsType<PdfDictionary>(graph.Resolve(rootReference.Id)!.Value);
        var updated = new PdfDictionary(catalog.Values.ToDictionary(static item => item.Key, static item => item.Value));
        update(updated);
        return new PdfIncrementalUpdateWriter().Append(
            source,
            [new PdfIncrementalObjectUpdate(rootReference.Id, updated)]).Bytes;
    }

    private static PdfDictionary Annotation(string subtype) => new()
    {
        ["Type"] = new PdfName("Annot"),
        ["Subtype"] = new PdfName(subtype),
        ["F"] = new PdfInteger(4),
        ["Rect"] = new PdfArray([
            new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(20)
        ])
    };

    private static PdfStreamObject AppearanceStream() => new(
        new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"),
            ["Subtype"] = new PdfName("Form"),
            ["BBox"] = new PdfArray([
                new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(20)
            ]),
            ["Length"] = new PdfInteger(0)
        },
        Array.Empty<byte>());
}
