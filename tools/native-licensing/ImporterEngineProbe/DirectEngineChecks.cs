using System.IO.Compression;
using System.Reflection;
using System.Text;
using PVAD.Importer;
using PVAD.Importer.Analysis;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Fonts;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Streams;
using PVAD.Importer.Tokenizer;
using PVAD.Importer.Xref;
using PVAD.Licensing;

internal static class DirectEngineChecks
{
    internal static int Run(string[] licenses, List<string> checks, byte[] pdf)
    {
        var calls = new Dictionary<string, Action>();
        var types = new HashSet<Type>();
        var context = new PdfParseContext(pdf, new PdfImporterOptions());
        var xref = new PdfCrossReferenceParser();
        var parser = new PdfObjectParser(new PdfParseContext("42"u8.ToArray(), new PdfImporterOptions()), new PdfCrossReferenceTable());
        var content = new PdfContentStreamParser();
        var interpreter = new PdfGraphicsInterpreter();
        var builder = new PdfDocumentBuilder(content, interpreter);
        var graph = new PdfObjectGraph();
        var resolver = new PdfObjectResolver(graph);
        var fontResolver = new CountingFontResolver();
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = new PdfName("Type1"), ["BaseFont"] = new PdfName("Helvetica") });
        var command = new PdfContentCommand(new("Tj", PdfOperatorCategory.TextShowing, 1), [new PdfString("Hello"u8.ToArray(), false)], default, 0);
        var text = new PdfTextElement(0, PdfMatrix.Identity, command, "Hello") { FontSize = 12 };
        var path = new PdfPathElement(1, PdfMatrix.Identity, command, [new RectangleSegment(new(0, 0, 20, 10))]);
        var image = new PdfImageElement(2, PdfMatrix.Identity, command, "Im1");
        var group = new PdfGroupElement(3, PdfMatrix.Identity, command);
        group.Children.Add(text);
        var page = new PdfPageModel(null, new PdfDictionary());
        page.GraphicsObjects.AddRange([text, path, image]);
        var document = new PdfDocumentModel();
        document.Pages.Add(page);
        var bounds = new BoundingBoxCalculator();
        var primitives = new PrimitiveBuilder();
        var snapshot = new PrimitiveGraphicsStateSnapshot(PdfColor.Black, PdfColor.Black, 0, null);
        var primitiveText = new PrimitiveText(text, new(0, 0, 30, 12), new(new(0, 0, 30, 12), 0, new(1, 0)), snapshot);
        PrimitiveObject[] objects = [primitiveText];
        var line = new ReadingLine(0, [primitiveText], primitiveText.Bounds);
        var paragraph = new ReadingParagraph(0, [line], line.Bounds);
        var order = new ReadingOrderEngine();
        var reconstruction = new TextReconstructionEngine();
        var scene = new SceneGraphEngine();
        var xobjects = new XObjectResolver(resolver);
        var form = new PdfStreamObject(new PdfDictionary(), "BT (Hello) Tj ET"u8.ToArray());
        var enumerations = 0;
        var cmap = Encoding.ASCII.GetBytes("1 beginbfchar\n<41> <0041>\nendbfchar");

        Add<PdfTokenizer>(nameof(PdfTokenizer.ReadToken), () => { var tokenizer = new PdfTokenizer("42"u8); Equal(tokenizer.ReadToken().Text == "42"); });
        Add<PdfTokenizer>(nameof(PdfTokenizer.Seek), () => { var tokenizer = new PdfTokenizer("42"u8); tokenizer.Seek(1); Equal(tokenizer.Position == 1); }, typeof(int));
        Add<PdfObjectParser>(nameof(PdfObjectParser.ParseObjectAt), () => Equal(parser.ParseObjectAt(0, 0) is PdfInteger { Value: 42 }), typeof(int), typeof(long));
        Add<PdfObjectParser>(nameof(PdfObjectParser.ParseDocumentGraph), () => parser.ParseDocumentGraph(), typeof(bool));
        Add<PdfObjectParser>(nameof(PdfObjectParser.TryParseIndirectObjectAt), () => parser.TryParseIndirectObjectAt(0), typeof(long));
        Add<PdfObjectParser>(nameof(PdfObjectParser.ParseObject), () => { var tokenizer = new PdfTokenizer("42"u8); Equal(parser.ParseObject(ref tokenizer) is PdfInteger { Value: 42 }); }, typeof(PdfTokenizer).MakeByRefType());
        Add<PdfCrossReferenceParser>(nameof(PdfCrossReferenceParser.Parse), () => Equal(xref.Parse(context).Entries.Count > 0), typeof(PdfParseContext));
        Add<PdfContentStreamParser>(nameof(PdfContentStreamParser.Parse), () => Equal(content.Parse("BT (Hello) Tj ET"u8.ToArray()).Count == 3), typeof(ReadOnlyMemory<byte>));
        Add<PdfDocumentBuilder>(nameof(PdfDocumentBuilder.Build), () => builder.Build(graph), typeof(PdfObjectGraph));
        Add<PdfGraphicsInterpreter>(nameof(PdfGraphicsInterpreter.Interpret), () => Equal(interpreter.Interpret([command]).Count == 1), typeof(IReadOnlyList<PdfContentCommand>));
        Add<PdfGraphicsInterpreter>(nameof(PdfGraphicsInterpreter.Interpret), () => Equal(interpreter.Interpret([command], null).Count == 1), typeof(IReadOnlyList<PdfContentCommand>), typeof(IReadOnlyDictionary<string, PdfFontResource>), typeof(IReadOnlyDictionary<string, PdfColorSpace>));
        Add<PdfSimpleFontParser>(nameof(PdfSimpleFontParser.Parse), () => Equal(new PdfSimpleFontParser().Parse("F1", font, fontResolver).Kind == PdfFontKind.Type1), typeof(string), typeof(PdfDictionary), typeof(IPdfObjectResolver));
        Add<PdfToUnicodeCMapParser>(nameof(PdfToUnicodeCMapParser.Parse), () => Equal(new PdfToUnicodeCMapParser().Parse(cmap).Decode(new byte[] { 65 }) == "A"), typeof(ReadOnlyMemory<byte>));
        Add<PrimitiveBuilder>(nameof(PrimitiveBuilder.Build), () => Equal(primitives.Build(Elements()).Count == 1), typeof(IEnumerable<PdfGraphicsElement>));
        Add<BoundingBoxCalculator>(nameof(BoundingBoxCalculator.ComputeTextGeometry), () => bounds.ComputeTextGeometry(text), typeof(PdfTextElement));
        Add<BoundingBoxCalculator>(nameof(BoundingBoxCalculator.ComputePathBounds), () => bounds.ComputePathBounds(path), typeof(PdfPathElement));
        Add<BoundingBoxCalculator>(nameof(BoundingBoxCalculator.ComputeImageBounds), () => bounds.ComputeImageBounds(image), typeof(PdfImageElement));
        Add<BoundingBoxCalculator>(nameof(BoundingBoxCalculator.ComputeGroupBounds), () => bounds.ComputeGroupBounds(group), typeof(PdfGroupElement));
        Add<BoundingBoxCalculator>(nameof(BoundingBoxCalculator.ComputeElementBounds), () => bounds.ComputeElementBounds(text), typeof(PdfGraphicsElement));
        Add<ReadingOrderEngine>(nameof(ReadingOrderEngine.BuildLines), () => Equal(order.BuildLines(Objects()).Count == 1), typeof(IEnumerable<PrimitiveObject>));
        Add<ReadingOrderEngine>(nameof(ReadingOrderEngine.BuildParagraphs), () => Equal(order.BuildParagraphs([line]).Count == 1), typeof(IEnumerable<ReadingLine>));
        Add<ReadingOrderEngine>(nameof(ReadingOrderEngine.DetectColumns), () => Equal(order.DetectColumns([paragraph]).Count == 1), typeof(IEnumerable<ReadingParagraph>));
        Add<ReadingOrderEngine>(nameof(ReadingOrderEngine.Analyze), () => Equal(order.Analyze(Objects()).Lines.Count == 1), typeof(IEnumerable<PrimitiveObject>));
        Add<GroupingEngine>(nameof(GroupingEngine.BuildGroups), () => new GroupingEngine().BuildGroups(objects), typeof(IReadOnlyList<PrimitiveObject>));
        Add<TextReconstructionEngine>(nameof(TextReconstructionEngine.BuildWords), () => Equal(reconstruction.BuildWords(line).Count > 0), typeof(ReadingLine));
        Add<TextReconstructionEngine>(nameof(TextReconstructionEngine.BuildParagraphText), () => Equal(reconstruction.BuildParagraphText(paragraph).Contains("Hello")), typeof(ReadingParagraph));
        Add<BarcodeDetector>(nameof(BarcodeDetector.DetectLinearBarcode), () => new BarcodeDetector().DetectLinearBarcode(Objects()), typeof(IEnumerable<PrimitiveObject>));
        Add<BarcodeDetector>(nameof(BarcodeDetector.DetectMatrixBarcode), () => new BarcodeDetector().DetectMatrixBarcode(Objects()), typeof(IEnumerable<PrimitiveObject>));
        Add<ObjectClassifier>(nameof(ObjectClassifier.Classify), () => new ObjectClassifier().Classify(objects), typeof(IReadOnlyList<PrimitiveObject>));
        Add<XObjectResolver>(nameof(XObjectResolver.Resolve), () => xobjects.Resolve("Absent", new PdfDictionary(), PdfMatrix.Identity), typeof(string), typeof(PdfDictionary), typeof(PdfMatrix), typeof(int));
        Add<XObjectResolver>(nameof(XObjectResolver.ResolveForm), () => Equal(xobjects.ResolveForm("Fm1", form, PdfMatrix.Identity).Children.Count == 1), typeof(string), typeof(PdfStreamObject), typeof(PdfMatrix), typeof(int));
        Add<XObjectResolver>(nameof(XObjectResolver.ResolveImage), () => Equal(xobjects.ResolveImage("Im1", form, PdfMatrix.Identity).Subtype == "Image"), typeof(string), typeof(PdfStreamObject), typeof(PdfMatrix));
        Add<SceneGraphEngine>(nameof(SceneGraphEngine.Build), () => Equal(scene.Build(document).Pages.Count == 1), typeof(PdfDocumentModel));
        Add<SceneGraphEngine>(nameof(SceneGraphEngine.BuildPage), () => Equal(scene.BuildPage(0, page).Layers.Count == 1), typeof(int), typeof(PdfPageModel));
        Add<SemanticLayoutEngine>(nameof(SemanticLayoutEngine.BuildPage), () => new SemanticLayoutEngine().BuildPage(0, objects, new ReadingOrderResult { Lines = [line], Paragraphs = [paragraph] }, [], null), typeof(int), typeof(IReadOnlyList<PrimitiveObject>), typeof(ReadingOrderResult), typeof(IReadOnlyList<VisualGroup>), typeof(PdfRectangle?));

        var registry = new PdfStreamDecoderRegistry();
        var customDecoder = new CountingStreamDecoder();
        registry.Add(customDecoder);
        var unfiltered = new PdfStreamObject(new PdfDictionary(), "AB"u8.ToArray());
        Add<PdfStreamDecoderRegistry>(nameof(PdfStreamDecoderRegistry.Decode), () => Equal(registry.Decode(unfiltered).Span.SequenceEqual("AB"u8)), typeof(PdfStreamObject));
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) zlib.Write("AB"u8);
        Decoder(new FlateDecodeStreamDecoder(), compressed.ToArray(), new PdfDictionary(), "AB"u8.ToArray());
        Decoder(new AsciiHexStreamDecoder(), "4142>"u8.ToArray(), new PdfDictionary(), "AB"u8.ToArray());
        Decoder(new Ascii85StreamDecoder(), "z~>"u8.ToArray(), new PdfDictionary(), new byte[4]);
        // Clear, A, B, EOD: four nine-bit LZW codes.
        Decoder(new LzwStreamDecoder(), [0x80, 0x10, 0x48, 0x50, 0x10], new PdfDictionary(), "AB"u8.ToArray());
        Decoder(new CcittFaxStreamDecoder(), [0xb0], new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Columns"] = new PdfInteger(4), ["Rows"] = new PdfInteger(1),
                ["EndOfLine"] = new PdfBoolean(false), ["EndOfBlock"] = new PdfBoolean(false),
            }),
        }), [0xf0]);

        var colorFont = new ColorFontChecks(calls, types);

        var methods = types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName && !ColorFontChecks.IsFreeMethod(method) && !(method.DeclaringType == typeof(PdfStreamDecoderRegistry) && method.Name is "Add" or "Evaluate") && method.Name != "CanDecode")
            .Select(Key).ToHashSet();
        Equal(methods.SetEquals(calls.Keys));
        checks.Add("direct:public-processing-inventory");
        Equal(registry.Evaluate(new PdfName("FlateDecode"))[0].Status == PdfStreamDecoderSupportStatus.Supported);
        Equal(resolver.Resolve(new PdfInteger(42)) is PdfInteger { Value: 42 });
        Equal(new PdfMatrix(1, 0, 0, 1, 0, 0).Multiply(PdfMatrix.Identity) == PdfMatrix.Identity);
        checks.Add("direct:metadata-model-and-geometry-free");
        colorFont.VerifyMetadata();
        var colorSnapshot = colorFont.Snapshot();
        foreach (var (name, call) in calls) Denied(call, "PVADLIC001", "direct:missing:" + name);
        colorFont.VerifyUnchanged(colorSnapshot);
        checks.Add("color-font:missing-license-preserves-inputs-and-callbacks");
        var customStream = new PdfStreamObject(new PdfDictionary(new Dictionary<string, PdfObject> { ["Filter"] = new PdfName(customDecoder.FilterName) }), "AB"u8.ToArray());
        Denied(() => registry.Decode(customStream), "PVADLIC001", "direct:missing:custom-decoder-dispatch");
        Equal(enumerations == 0 && fontResolver.Calls == 0 && customDecoder.Calls == 0 && text.Bounds is null && path.Bounds is null);
        checks.Add("direct:missing-license-preserves-inputs");
        var tokenizerState = default(PdfTokenizer);
        try { tokenizerState.ReadToken(); throw new InvalidOperationException("Default tokenizer bypassed admission."); }
        catch (PvadLicenseException error) when (error.Code == "PVADLIC001") { checks.Add("direct:default-tokenizer-guarded"); }
        PvadLicense.LoadFromFile(licenses[0]);
        foreach (var (name, call) in calls) { call(); checks.Add("direct:valid:" + name); }
        colorFont.VerifyFunctionFamilies(checks);
        colorFont.VerifyNestedDenials(licenses, checks);
        Equal(registry.Decode(customStream).Span.SequenceEqual("AB"u8) && customDecoder.Calls == 1);
        checks.Add("direct:licensed-custom-decoder-dispatch");
        var parsedGraph = new PdfObjectParser(context, xref.Parse(context)).ParseDocumentGraph();
        var built = builder.Build(parsedGraph);
        Equal(built.Pages.Count == 1 && built.Pages[0].TextObjects.Any(element => element.Text.Contains("Imported content", StringComparison.Ordinal)));
        checks.Add("direct:licensed-parse-build-preserves-real-pdf-content");
        Equal(enumerations > 0 && fontResolver.Calls > 0);
        checks.Add("direct:licensed-enumeration-and-font-resolution");
        var retained = new PdfTokenizer("42 99"u8);
        Equal(retained.ReadToken().Text == "42");
        var position = retained.Position;
        text.Bounds = null;
        path.Bounds = null;
        var classification = primitiveText.Classification;
        var originalEnumerations = enumerations;
        var originalResolutions = fontResolver.Calls;
        var originalSource = pdf.ToArray();
        colorSnapshot = colorFont.Snapshot();
        PvadLicense.LoadFromFile(licenses[1]);
        foreach (var (name, call) in calls) Denied(call, "PVADLIC012", "direct:wrong:" + name);
        colorFont.VerifyUnchanged(colorSnapshot);
        colorFont.VerifyMetadata();
        checks.Add("color-font:wrong-license-preserves-inputs-and-callbacks");
        colorFont.VerifyRetainedDenials(checks);
        Denied(() => registry.Decode(customStream), "PVADLIC012", "direct:wrong:custom-decoder-dispatch");
        try { retained.ReadToken(); throw new InvalidOperationException("Retained tokenizer bypassed admission."); }
        catch (PvadLicenseException error) when (error.Code == "PVADLIC012") { Equal(retained.Position == position); checks.Add("direct:retained-tokenizer-read-preserves-position"); }
        try { retained.Seek(0); throw new InvalidOperationException("Retained tokenizer seek bypassed admission."); }
        catch (PvadLicenseException error) when (error.Code == "PVADLIC012") { Equal(retained.Position == position); checks.Add("direct:retained-tokenizer-seek-preserves-position"); }
        Equal(enumerations == originalEnumerations && fontResolver.Calls == originalResolutions && customDecoder.Calls == 1 && text.Bounds is null && path.Bounds is null && primitiveText.Classification == classification && pdf.SequenceEqual(originalSource));
        checks.Add("direct:wrong-license-preserves-inputs-and-callbacks");
        Denied(() => PvadLicense.LoadFromFile(licenses[2]), "PVADLIC009", "direct:expired-replacement-rejected");
        Denied(calls.Values.First(), "PVADLIC012", "direct:failed-renewal-preserves-snapshot");
        PvadLicense.LoadFromFile(licenses[0]);
        Equal(retained.ReadToken().Text == "99");
        foreach (var (name, call) in calls) { call(); checks.Add("direct:renewed:" + name); }
        colorFont.VerifyRetainedRenewal(checks);
        checks.Add("direct:renewal-restores-retained-tokenizer");
        return calls.Count;

        void Add<T>(string name, Action call, params Type[] parameters) where T : allows ref struct => AddMethod(typeof(T), name, parameters, call);
        void AddMethod(Type type, string name, Type[] parameters, Action call)
        {
            types.Add(type);
            var method = type.GetMethod(name, parameters) ?? throw new InvalidOperationException("Missing processing entry.");
            calls.Add(Key(method), call);
        }
        void Decoder(IPdfStreamDecoder decoder, byte[] bytes, PdfDictionary dictionary, byte[] expected)
        {
            Equal(decoder.CanDecode(new PdfName(decoder.FilterName)));
            AddMethod(decoder.GetType(), "Decode", [typeof(ReadOnlyMemory<byte>), typeof(PdfDictionary)], () => Equal(decoder.Decode(bytes, dictionary).Span.SequenceEqual(expected)));
        }
        IEnumerable<PdfGraphicsElement> Elements() { enumerations++; yield return text; }
        IEnumerable<PrimitiveObject> Objects() { enumerations++; yield return primitiveText; }
        void Denied(Action call, string code, string name)
        {
            try { call(); }
            catch (PvadLicenseException error) when (error.Code == code) { checks.Add(name); return; }
            throw new InvalidOperationException("Expected license denial: " + name);
        }
    }

    private static string Key(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}({string.Join(',', method.GetParameters().Select(parameter => parameter.ParameterType.ToString()))})";
    private static void Equal(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Direct engine admission/content check failed.");
    }

    private sealed class CountingFontResolver : IPdfObjectResolver
    {
        public int Calls { get; private set; }
        public PdfObject? Resolve(PdfObject value) { Calls++; return value; }
    }

    private sealed class CountingStreamDecoder : IPdfStreamDecoder
    {
        public int Calls { get; private set; }
        public string FilterName => "SyntheticProbe";
        public bool CanDecode(PdfName filterName) => filterName.Value == FilterName;
        public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> bytes, PdfDictionary dictionary) { Calls++; return bytes; }
    }
}
