using PVAD.FileImporter;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Infrastructure;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;

internal static class FreeSurfaceChecks
{
    internal static async Task Run(string phase, List<string> checks)
    {
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Free API unexpectedly changed: " + name);
            checks.Add("free-surface:" + phase + ":" + name);
        }
        var value = new PdfInteger(42);
        var id = new PdfObjectId(1, 0);
        var graph = new PdfObjectGraph();
        graph.Add(new(id, value, default));
        var resolver = new PdfObjectResolver(graph);
        Check(resolver.TryResolve<PdfInteger>(new PdfReference(id), out var found) && found.Value == 42 && found.OriginalId == id &&
            ReferenceEquals(resolver.ResolveIndirect(new(id))!.Value, found), "detached-graph-and-resolution");
        var page = new PdfPageModel(null, new PdfDictionary());
        var command = new PdfContentCommand(new("Tj", PdfOperatorCategory.TextShowing, 1), [], default, 0);
        var text = new PdfTextElement(0, PdfMatrix.Identity, command, "Detached data");
        page.Insert(text); page.Delete(text);
        var document = new PdfDocumentModel(); document.AddPage(page);
        Check(document.Pages.Single().GraphicsObjects.Single().IsDeleted, "detached-page-data-mutation");
        var array = new PdfArray(); array.Add(value);
        var data = new PdfStreamObject(new(), "raw"u8.ToArray()); data.SetDecodedBytes("plain"u8.ToArray());
        Check(array.Items.Single() == value && data.DecodedBytes.Span.SequenceEqual("plain"u8), "detached-object-values");
        Check(new PdfString("raw"u8.ToArray(), false).ToLatin1String() == "raw", "string-value-conversion");
        Check(PdfOperatorRegistry.TryGet("Tj", out var descriptor) && descriptor.Name == "Tj", "operator-metadata");
        var stack = new GraphicsStateStack(); stack.Save();
        stack.Update(state => state with { FontSize = 24 }); stack.Restore();
        Check(stack.Current.FontSize == 0 && MatrixEngine.TransformPoint(new(2, 3), MatrixEngine.Translate(4, 5)) == new PdfPoint(6, 8),
            "detached-state-and-geometry");
        using var source = new MemoryStream("not a document"u8.ToArray());
        using (var buffer = await PdfBuffer.FromStreamAsync(source, CancellationToken.None))
            Check(buffer.Memory.Span.SequenceEqual("not a document"u8), "raw-buffer-does-not-parse-documents");
        Check(source.CanRead, "raw-buffer-leaves-input-open");
        using var remote = new SafeRemoteImageResolver();
        Check(await remote.ResolveAsDataUrlAsync("not an absolute URI", CancellationToken.None) is null,
            "asset-adapter-invalid-uri-without-network");
    }
}
