using System.Collections;
using System.Reflection;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Editing;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Licensing;

internal sealed class EditingChecks : IDisposable
{
    private readonly Dictionary<string, Action> calls = [];
    private readonly List<PdfEditableDocument> documents = [];
    private readonly Dictionary<PdfEditableDocument, (byte[] Bytes, Exception? Error)> snapshots = [];
    private readonly PdfEditableDocument sourceDocument = new();
    private readonly PdfPageComposer composer = new();
    private readonly PdfPageEditor editor = new();
    private readonly string directory = Directory.CreateTempSubdirectory("pvad-editing-admission-").FullName;
    private readonly string inputPath;
    private readonly string outputPath;
    private readonly byte[] original;
    private byte[] pages;
    private bool denying = true;
    private readonly PdfDocumentModel model = new();
    private readonly PdfPageModel page = new(null, new PdfDictionary());
    private readonly PdfTextElement text;
    private readonly PdfEditingSession session;
    private readonly CountingList<PdfPageEditOperation> edits = new([new PdfMovePageOperation(1, 2)]);
    private readonly CountingList<PdfPageImport> imports;
    private (string Text, PdfMatrix Transform, bool Deleted, int Count, long? Metadata) sessionSnapshot;
    private Dictionary<string, byte[]> files = [];
    private int reads;

    internal EditingChecks(byte[] pdf)
    {
        original = pdf.ToArray();
        pages = pdf;
        inputPath = Path.Combine(directory, "source.pdf");
        outputPath = Path.Combine(directory, "output.pdf");
        File.WriteAllBytes(inputPath, pdf);
        File.WriteAllBytes(outputPath, pdf);
        imports = new([new PdfPageImport(pdf, 1)]);
        text = new PdfTextElement(0, PdfMatrix.Identity, new(new("Tj", PdfOperatorCategory.TextShowing, 1), [], default, 0), "Before");
        model.AddPage(page);
        session = new PdfEditingSession(model);

        Add<PdfEditingSession>(nameof(PdfEditingSession.Insert), () => session.Insert(page, text), typeof(PdfPageModel), typeof(PdfGraphicsElement));
        Add<PdfEditingSession>(nameof(PdfEditingSession.ReplaceText), () => session.ReplaceText(text, "After"), typeof(PdfTextElement), typeof(string));
        Add<PdfEditingSession>(nameof(PdfEditingSession.Move), () => session.Move(text, new(1, 0, 0, 1, 10, 20)), typeof(PdfGraphicsElement), typeof(PdfMatrix));
        Add<PdfEditingSession>(nameof(PdfEditingSession.Delete), () => session.Delete(text), typeof(PdfGraphicsElement));
        Add<PdfEditingSession>(nameof(PdfEditingSession.SetMetadata), () => session.SetMetadata("NativeEdit", new PdfInteger(42)), typeof(string), typeof(PdfObject));
        var rewriter = new PdfContentStreamRewriter();
        var rewritePage = new PdfPageModel(null, new PdfDictionary());
        rewritePage.GraphicsObjects.Add(new PdfTextElement(0, PdfMatrix.Identity, text.SourceCommand, "Retained") { FontResourceName = "F1", FontSize = 12 });
        Add<PdfContentStreamRewriter>(nameof(PdfContentStreamRewriter.Rewrite), () => Equal(rewriter.Rewrite(rewritePage).Length > 0), typeof(PdfPageModel));
        Add<PdfContentStreamRewriter>(nameof(PdfContentStreamRewriter.Rewrite), () => Equal(rewriter.Rewrite(RewriteElements()).Length > 0), typeof(IEnumerable<PdfGraphicsElement>));
        Add<PdfPageEditor>(nameof(PdfPageEditor.Apply), () => Equal(editor.Apply(pages, edits).PageCount == 2), typeof(ReadOnlyMemory<byte>), typeof(IReadOnlyList<PdfPageEditOperation>), typeof(PdfPageEditOptions));
        Add<PdfPageEditor>(nameof(PdfPageEditor.ReadPageBox), () => Equal(editor.ReadPageBox(pages, 1, PdfPageBoxKind.CropBox).Right == 300), typeof(ReadOnlyMemory<byte>), typeof(int), typeof(PdfPageBoxKind), typeof(PVAD.Importer.Parsing.PdfParseLimits));
        Add<PdfPageComposer>(nameof(PdfPageComposer.Apply), () => Equal(composer.Apply(pages, imports).PageCount == 3), typeof(ReadOnlyMemory<byte>), typeof(IReadOnlyList<PdfPageImport>), typeof(PdfPageCompositionOptions));
        Add<PdfPageComposer>(nameof(PdfPageComposer.Merge), () => Equal(composer.Merge([pages, pages]).PageCount == 4), typeof(IReadOnlyList<ReadOnlyMemory<byte>>), typeof(PdfPageCompositionOptions));
        Add<PdfPageComposer>(nameof(PdfPageComposer.AppendPages), () => Equal(composer.AppendPages(pages, pages, [2, 1]).PageCount == 4), typeof(ReadOnlyMemory<byte>), typeof(ReadOnlyMemory<byte>), typeof(IReadOnlyList<int>), typeof(PdfPageCompositionOptions));
        Add<PdfPageComposer>(nameof(PdfPageComposer.Split), () => Equal(composer.Split(pages, [new(1, 1), new(2, 2)]).Documents.All(bytes => composer.GetPageCount(bytes) == 1)), typeof(ReadOnlyMemory<byte>), typeof(IReadOnlyList<PdfPageRange>), typeof(PdfPageCompositionOptions));
        Add<PdfPageComposer>(nameof(PdfPageComposer.ExtractPages), () => Equal(composer.GetPageCount(composer.ExtractPages(pages, [2])) == 1), typeof(ReadOnlyMemory<byte>), typeof(IReadOnlyList<int>), typeof(PdfPageCompositionOptions));
        Add<PdfPageComposer>(nameof(PdfPageComposer.GetPageCount), () => Equal(composer.GetPageCount(pages) == 2), typeof(ReadOnlyMemory<byte>), typeof(PVAD.Importer.Parsing.PdfParseLimits));

        Facade("NewDocument", d => Success(d.NewDocument()));
        Facade("LoadFromFile", d => Success(d.LoadFromFile(inputPath)), typeof(string), typeof(bool));
        Facade("LoadFromStream", d => Read(stream => Success(d.LoadFromStream(stream, ownStream: true))), typeof(Stream), typeof(bool));
        Facade("MovePage", d => Success(d.MovePage(1, 2)), typeof(int), typeof(int));
        Facade("DeletePage", d => Success(d.DeletePage(1)), typeof(int));
        Facade("SelectPage", d => Success(d.SelectPage(2)), typeof(int));
        Facade("SetMeasurementUnit", d => Success(d.SetMeasurementUnit(PdfEditableMeasurementUnit.Inch)), typeof(PdfEditableMeasurementUnit));
        Facade("SetPageBox", d => Success(d.SetPageBox(PdfEditablePageBox.CropBox, 10, 150, 250, 10)), typeof(PdfEditablePageBox), typeof(double), typeof(double), typeof(double), typeof(double));
        Facade("RemovePageBox", d => Success(d.RemovePageBox(PdfEditablePageBox.CropBox)), typeof(PdfEditablePageBox));
        Facade("GetPageBox", GetBox, typeof(PdfEditablePageBox), typeof(float).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float).MakeByRefType());
        Facade("ClonePage", d => Success(d.ClonePage(sourceDocument, 1)), typeof(PdfEditableDocument), typeof(int));
        Facade("ClonePage", d => Success(d.ClonePage(1)), typeof(int));
        Facade("InsertBlankPage", d => Success(d.InsertBlankPage(120, 160, 1)), typeof(double), typeof(double), typeof(int));
        Facade("InsertPage", d => Success(d.InsertPage(PdfEditablePageSize.A4, 1)), typeof(PdfEditablePageSize), typeof(int));
        Facade("InsertPage", d => Success(d.InsertPage(100, 200, 1)), typeof(double), typeof(double), typeof(int));
        Facade("ClonePages", d => Success(d.ClonePages(sourceDocument, "1-2")), typeof(PdfEditableDocument), typeof(string));
        Facade("MergeDocuments", d => { using var merged = d.MergeDocuments(sourceDocument, sourceDocument); Equal(merged.GetPageCount() == 4); }, typeof(PdfEditableDocument[]));
        Facade("MergeDocuments", d => { using var merged = d.MergeDocuments([sourceDocument], PdfEditableMergeOptions.ExcludeAttachments); Equal(merged.GetStatus() == PdfPageOperationStatus.Failed && merged.LastError is NotSupportedException); }, typeof(PdfEditableDocument[]), typeof(PdfEditableMergeOptions));
        Facade("MergeDocuments", d => { Success(d.MergeDocuments([inputPath, inputPath], outputPath)); Equal(composer.GetPageCount(File.ReadAllBytes(outputPath)) == 2); }, typeof(string[]), typeof(string));
        Facade("Merge2Documents", d => { using var merged = d.Merge2Documents(sourceDocument, sourceDocument); Equal(merged.GetPageCount() == 4); }, typeof(PdfEditableDocument), typeof(PdfEditableDocument));
        Facade("Merge2Documents", d => Success(d.Merge2Documents(inputPath, inputPath, outputPath)), typeof(string), typeof(string), typeof(string));
        Facade("SaveToFile", d => Success(d.SaveToFile(outputPath)), typeof(string));
        Facade("ConfigureAes256", d => Success(d.ConfigureAes256("synthetic-owner", "synthetic-reader")), typeof(string), typeof(string));
        Facade("GetPageCount", d => Equal(d.GetPageCount() == 2));
        Facade("ToBytes", d => Equal(d.ToBytes().AsSpan().StartsWith("%PDF-"u8)));

        var paid = new[] { typeof(PdfEditingSession), typeof(PdfPageEditor), typeof(PdfPageComposer), typeof(PdfEditableDocument), typeof(PdfContentStreamRewriter) };
        var free = new[] { typeof(PdfEditableDocument).GetMethod("Dispose")!, typeof(PdfEditableDocument).GetMethod("GetStatus")! }.Select(Key);
        var methods = paid.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Where(method => !method.IsSpecialName).Select(Key).ToHashSet();
        Equal(methods.SetEquals(calls.Keys.Concat(free)));
        var dto = new[] { typeof(PdfPageOperationStatus), typeof(PdfEditablePageSize), typeof(PdfEditablePageBox), typeof(PdfEditableMeasurementUnit), typeof(PdfEditableMergeOptions), typeof(PdfPageEditOperation), typeof(PdfDeletePageOperation), typeof(PdfMovePageOperation), typeof(PdfPageBoxKind), typeof(PdfSetPageBoxOperation), typeof(PdfRemovePageBoxOperation), typeof(PdfPageEditOptions), typeof(PdfPageEditResult), typeof(PdfPageBoxCoordinates), typeof(PdfPageImport), typeof(PdfPageRange), typeof(PdfDocumentSplitResult), typeof(PdfPageCompositionOptions), typeof(PdfPageCompositionResult), typeof(PdfPageRangeParser) };
        Equal(typeof(PdfPageEditor).Assembly.GetExportedTypes().Where(type => type.Namespace == typeof(PdfPageEditor).Namespace).ToHashSet().SetEquals(paid.Where(type => type.Namespace == typeof(PdfPageEditor).Namespace).Concat(dto)));

        void Add<T>(string name, Action action, params Type[] parameters)
        {
            var method = typeof(T).GetMethod(name, parameters)!;
            calls.Add(Key(method), action);
            NativeSurfaceCoverage.Record(method);
        }
        IEnumerable<PdfGraphicsElement> RewriteElements() { reads++; yield return rewritePage.GraphicsObjects[0]; }
        void Facade(string name, Action<PdfEditableDocument> action, params Type[] parameters)
        {
            var document = new PdfEditableDocument();
            documents.Add(document);
            Add<PdfEditableDocument>(name, () => action(document), parameters);
        }
    }

    internal void CheckMissing(List<string> checks)
    {
        var before = SessionState();
        Equal(ReferenceEquals(session.Document, model) && documents.All(d => !d.IsLoaded && d.GetStatus() == PdfPageOperationStatus.Success && d.LastError is null));
        Equal(PdfPageRangeParser.Parse("1-2", 2).SequenceEqual(new[] { 1, 2 }));
        using (var cleanup = new PdfEditableDocument()) cleanup.Dispose();
        checks.Add("editing:metadata-ranges-and-disposal-free");
        checks.Add("editing:public-method-and-type-inventory");
        foreach (var (name, call) in calls) Denied(call, "PVADLIC001", "editing:missing:" + name, checks);
        Equal(reads == 0 && edits.Reads == 0 && imports.Reads == 0 && SessionState() == before);
        Equal(File.ReadAllBytes(inputPath).SequenceEqual(original) && File.ReadAllBytes(outputPath).SequenceEqual(original));
        checks.Add("editing:missing-license-preserves-inputs-and-session");
    }

    internal int CheckLicensed(string[] licenses, List<string> checks)
    {
        Prepare();
        foreach (var (name, call) in calls) { call(); checks.Add("editing:valid:" + name); }
        Equal(edits.Reads > 0 && imports.Reads > 0 && reads > 0);
        VerifyLoadFailures(checks);
        using (var blank = new PdfEditableDocument())
        {
            Success(blank.InsertBlankPage(123, 234, 1));
            Equal(editor.ReadPageBox(blank.ToBytes(), 1, PdfPageBoxKind.CropBox) == new PdfPageBoxCoordinates(0, 234, 123, 0));
            Success(blank.NewDocument());
            Success(blank.ClonePage(sourceDocument, 1));
            Equal(blank.GetPageCount() == 1);
            Success(blank.NewDocument());
            Success(blank.ClonePages(sourceDocument, "1-2"));
            Equal(blank.GetPageCount() == 2);
        }
        checks.Add("editing:importer-only-blank-clone-and-extract");
        var writer = typeof(PVAD.Pdf.PdfDocument).Assembly.GetType("PVAD.Pdf.Serialization.PdfWriter", true)!;
        Equal(writer.GetMethod("CreateBlankPageForImporter", BindingFlags.Public | BindingFlags.Static) is null && writer.GetMethod("WriteCore", BindingFlags.Public | BindingFlags.Instance) is null);
        Equal(typeof(PVAD.Pdf.PdfDocument).GetConstructors().Length == 1);
        checks.Add("editing:no-public-generator-license-exemption");

        Prepare();
        foreach (var document in documents)
        {
            Success(document.SelectPage(2));
            Success(document.SetMeasurementUnit(PdfEditableMeasurementUnit.Inch));
            Success(document.SetPageBox(PdfEditablePageBox.CropBox, 0.1, 2, 3, 0.2));
            Equal(document.DeletePage(99) == PdfPageOperationStatus.Failed);
            snapshots.Add(document, (document.ToBytes(), document.LastError));
        }
        files = Directory.GetFiles(directory).ToDictionary(path => path, File.ReadAllBytes);
        sessionSnapshot = SessionState();
        var originalReads = reads + edits.Reads + imports.Reads;
        denying = true;
        PvadLicense.LoadFromFile(licenses[1]);
        foreach (var (name, call) in calls) Denied(call, "PVADLIC012", "editing:wrong:" + name, checks);
        Equal(SessionState() == sessionSnapshot && originalReads == reads + edits.Reads + imports.Reads);
        Equal(documents.All(d => d.IsLoaded && ReferenceEquals(d.LastError, snapshots[d].Error) && d.GetStatus() == PdfPageOperationStatus.Failed));
        foreach (var (path, bytes) in files) Equal(File.ReadAllBytes(path).SequenceEqual(bytes));
        var absent = Path.Combine(directory, "absent.pdf");
        Denied(() => documents[0].SaveToFile(absent), "PVADLIC012", "editing:denied-save-does-not-create-file", checks);
        Equal(!File.Exists(absent));
        checks.Add("editing:denials-preserve-model-status-files-and-ref-parameters");
        Denied(() => PvadLicense.LoadFromFile(licenses[2]), "PVADLIC009", "editing:expired-renewal-rejected", checks);
        Denied(() => documents[0].NewDocument(), "PVADLIC012", "editing:failed-renewal-preserves-snapshot", checks);
        PvadLicense.LoadFromFile(licenses[0]);
        denying = false;
        foreach (var document in documents)
        {
            Equal(document.ToBytes().SequenceEqual(snapshots[document].Bytes));
            float l = 0, t = 0, r = 0, b = 0;
            Success(document.GetPageBox(PdfEditablePageBox.CropBox, ref l, ref t, ref r, ref b));
            Equal(Math.Abs(l - 0.1) < 0.0001 && Math.Abs(t - 2) < 0.0001 && Math.Abs(r - 3) < 0.0001 && Math.Abs(b - 0.2) < 0.0001);
        }
        checks.Add("editing:renewal-confirms-bytes-selected-page-and-units-unchanged");
        Prepare();
        foreach (var (name, call) in calls) { call(); checks.Add("editing:renewed:" + name); }
        return calls.Count;
    }

    private void Prepare()
    {
        denying = false;
        pages = composer.Merge([original, original]).Bytes;
        using (var input = new MemoryStream(pages)) Success(sourceDocument.LoadFromStream(input));
        foreach (var document in documents)
        {
            using var input = new MemoryStream(pages);
            Success(document.LoadFromStream(input));
        }
    }

    private void VerifyLoadFailures(List<string> checks)
    {
        var document = documents[0];
        using (var input = new MemoryStream(pages)) Success(document.LoadFromStream(input));
        using (var invalid = new MemoryStream(new byte[] { 1, 2, 3 })) Equal(document.LoadFromStream(invalid) == PdfPageOperationStatus.Failed);
        Equal(document.ToBytes().SequenceEqual(pages));
        var invalidPath = Path.Combine(directory, "invalid.pdf");
        File.WriteAllBytes(invalidPath, [1, 2, 3]);
        Equal(document.LoadFromFile(invalidPath) == PdfPageOperationStatus.Failed && document.ToBytes().SequenceEqual(pages));
        checks.Add("editing:invalid-file-and-stream-do-not-replace-loaded-document");
    }

    private void Read(Action<Stream> call)
    {
        using var input = new MemoryStream(pages);
        try
        {
            call(input);
            if (!denying) { Equal(!input.CanRead); reads++; }
        }
        finally { if (denying) Equal(input.CanRead && input.Position == 0 && input.ToArray().SequenceEqual(pages)); }
    }

    private void GetBox(PdfEditableDocument document)
    {
        float l = 11, t = 22, r = 33, b = 44;
        try { Success(document.GetPageBox(PdfEditablePageBox.CropBox, ref l, ref t, ref r, ref b)); }
        finally { if (denying) Equal(l == 11 && t == 22 && r == 33 && b == 44); }
    }

    private (string, PdfMatrix, bool, int, long?) SessionState() => (text.Text, text.Transform, text.IsDeleted, page.GraphicsObjects.Count, (model.Metadata["NativeEdit"] as PdfInteger)?.Value);
    private static void Success(PdfPageOperationStatus status) => Equal(status == PdfPageOperationStatus.Success);
    private static string Key(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}({string.Join(',', method.GetParameters().Select(parameter => parameter.ParameterType.ToString()))})";
    private static void Equal(bool value)
    {
        if (!value) throw new InvalidOperationException("Editing admission/state check failed.");
    }
    private static void Denied(Action call, string code, string name, List<string> checks)
    {
        try { call(); }
        catch (PvadLicenseException error) when (error.Code == code) { checks.Add(name); return; }
        throw new InvalidOperationException("Expected editing license denial: " + name);
    }
    public void Dispose()
    {
        foreach (var document in documents) document.Dispose();
        sourceDocument.Dispose();
        Directory.Delete(directory, recursive: true);
    }
    private sealed class CountingList<T>(IReadOnlyList<T> values) : IReadOnlyList<T>
    {
        public int Reads { get; private set; }
        public int Count { get { Reads++; return values.Count; } }
        public T this[int index] { get { Reads++; return values[index]; } }
        public IEnumerator<T> GetEnumerator() { Reads++; return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
