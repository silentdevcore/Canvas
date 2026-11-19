using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.FileImporter;
using PVAD.Licensing;

internal static class FileImporterContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks, Dictionary<string, byte[]> fixtures)
    {
        var start = checks.Count;
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), "product-metadata/native-file-importer-entries.v1.json")));
        var readers = FileImporterRegistry.Keys.ToDictionary(key => key, FileImporterRegistry.Create);
        var types = typeof(IFileImporter).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(IFileImporter).IsAssignableFrom(type)).ToHashSet();
        Check(types.SetEquals(readers.Values.Select(reader => reader.GetType())), "registry-covers-all-public-implementations");
        var entries = inventory.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Check(entries.Length == readers.Count && entries.Select(entry => entry.GetProperty("key").GetString()).ToHashSet().SetEquals(readers.Keys),
            "inventory-matches-registry");
        var calls = new Dictionary<string, (string Key, Func<Stream, Task<DesignExportDto>> Import)>();
        foreach (var entry in entries)
        {
            var key = entry.GetProperty("key").GetString()!;
            var reader = readers[key];
            var type = reader.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName).ToArray();
            Check(type.Name == entry.GetProperty("type").GetString() && methods.Length == entry.GetProperty("publicProcessingOverloads").GetInt32(),
                key + ":public-overload-inventory");
            if (key == "pdf") continue; // The PDF-specific probe exercises its parser/analysis continuations.
            foreach (var method in methods)
            {
                NativeSurfaceCoverage.Record(method);
                Check(method.ReturnType == typeof(DesignExportDto) || method.ReturnType == typeof(Task<DesignExportDto>),
                    key + ":return-contract:" + Signature(method));
                calls.Add(key + ":" + Signature(method), (key, async stream =>
                {
                    object? result;
                    try
                    {
                        result = method.Invoke(method.IsStatic ? null : reader, method.GetParameters().Select(parameter =>
                            parameter.ParameterType == typeof(Stream) ? (object)stream :
                            parameter.ParameterType == typeof(string) ? "Synthetic import.png" :
                            parameter.ParameterType == typeof(CancellationToken) ? CancellationToken.None :
                            parameter.ParameterType == typeof(Uri) ? new Uri("https://example.com/") :
                            throw new InvalidOperationException("Unclassified importer argument: " + parameter)).ToArray());
                    }
                    catch (TargetInvocationException error) when (error.InnerException is not null)
                    {
                        ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                        throw;
                    }
                    return result is Task<DesignExportDto> pending ? await pending : (DesignExportDto)result!;
                }));
            }
            calls.Add(key + ":registry", (key, stream => FileImporterRegistry.Create(key).ImportAsync(stream, "Synthetic import.png")));
            calls.Add(key + ":interface-cancellation", (key, stream => reader.ImportAsync(stream, "Synthetic import.png", CancellationToken.None)));
        }
        Check(calls.Count == 30, "all-non-pdf-direct-registry-and-interface-calls");
        foreach (var (name, (key, import)) in calls)
        {
            var bytes = fixtures[key];
            var hash = SHA256.HashData(bytes);
            PvadLicense.LoadFromFile(licenses[0]);
            using var baselineInput = new MemoryStream(bytes);
            var baseline = await import(baselineInput);
            Check(baseline.Pages.Count > 0 && baseline.Pages.SelectMany(page => page.Elements).Any(), name + ":baseline-has-content");
            var expected = Fingerprint(baseline, key);
            foreach (var seekable in new[] { false, true })
            foreach (var yieldRead in new[] { false, true })
            {
                var phase = name + ":" + seekable + ":" + yieldRead;
                PvadLicense.LoadFromFile(licenses[0]);
                using var source = new SwitchingStream(bytes, seekable, yieldRead, () =>
                {
                    PvadLicense.LoadFromFile(licenses[1]);
                    Denied(() => MarkdownFileImporter.Import(Stream.Null), phase + ":reentrant-public-import-denied");
                });
                var result = await import(source);
                Check(source.Reads > 0 && source.CanRead, phase + ":source-left-open");
                Check(hash.SequenceEqual(SHA256.HashData(bytes)), phase + ":source-bytes-unchanged");
                Check(Fingerprint(result, key) == expected, phase + ":identical-content-geometry-and-diagnostics");
                using var rejected = new SwitchingStream(bytes, seekable, yieldRead, () => throw new InvalidOperationException("Denied import read input."));
                try { await import(rejected); throw new InvalidOperationException("New import reused prior admission."); }
                catch (PvadLicenseException error) when (error.Code == "PVADLIC012")
                { Check(rejected.Reads == 0 && rejected.CanRead, phase + ":next-public-import-denied-before-read"); }
            }
        }

        // A user-supplied resolver must not acquire reusable native rights across awaits.
        foreach (var fail in new[] { false, true })
        {
            var phase = "markdown-resolver:" + fail;
            PvadLicense.LoadFromFile(licenses[0]);
            var resolver = new SwitchingResolver(async () =>
            {
                await Task.Yield();
                PvadLicense.LoadFromFile(licenses[1]);
                Denied(() => ImageFileImporter.Import(Stream.Null, "test.png"), phase + ":public-native-import-denied");
                if (fail) throw new IOException("Synthetic resolver failure.");
                return "data:image/png;base64," + Convert.ToBase64String(fixtures["image"]);
            });
            using var source = new MemoryStream(fixtures["md"]);
            var importer = new MarkdownFileImporter(resolver);
            try
            {
                var result = await importer.ImportAsync(source, "Synthetic Markdown", new Uri("https://example.com/"), CancellationToken.None);
                Check(!fail && result.Pages.SelectMany(page => page.Elements).Single(element => element.Type == "image").Content!
                    .EndsWith(Convert.ToBase64String(fixtures["image"]), StringComparison.Ordinal), phase + ":asset-and-design-returned");
            }
            catch (IOException error) when (fail && error.Message == "Synthetic resolver failure.")
            { Check(true, phase + ":original-callback-error-preserved"); }
            Check(resolver.Calls == 1 && source.CanRead, phase + ":one-callback-and-open-source");
            Denied(() => MarkdownFileImporter.Import(Stream.Null), phase + ":no-admission-after-return-or-error");
            PvadLicense.LoadFromFile(licenses[0]);
            using var renewed = new MemoryStream(fixtures["md"]);
            Check(MarkdownFileImporter.Import(renewed).Pages.Count > 0, phase + ":renewal-restores-public-work");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        return checks.Count - start;

        void Check(bool condition, string name)
        {
            name = "file-importer-continuation:" + name;
            if (!condition || checks.Contains(name)) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        void Denied(Action action, string name)
        {
            try { action(); }
            catch (PvadLicenseException error) when (error.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected license denial: " + name);
        }
    }

    private static string Signature(MethodInfo method) => method.Name + "(" +
        string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")";

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VERSION"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Fingerprint(DesignExportDto design, string key)
    {
        var node = JsonSerializer.SerializeToNode(design)!.AsObject();
        var id = design.Id!;
        if (!(id.Length == 12 && id.All(Uri.IsHexDigit)) && !Guid.TryParse(id, out _))
            throw new InvalidOperationException("Unexpected generated document ID.");
        node["Id"] = "generated-document";
        if (key == "image")
        {
            foreach (var page in node["Pages"]!.AsArray())
            {
                if (!Guid.TryParse(page!["Id"]!.GetValue<string>(), out _)) throw new InvalidOperationException("Unexpected image page ID.");
                page["Id"] = "generated-image-page";
                foreach (var element in page["Elements"]!.AsArray())
                {
                    if (!Guid.TryParse(element!["Id"]!.GetValue<string>(), out _)) throw new InvalidOperationException("Unexpected image element ID.");
                    element["Id"] = "generated-image-element";
                }
            }
        }
        return node.ToJsonString();
    }

    private sealed class SwitchingResolver(Func<Task<string?>> resolve) : IRemoteImageResolver
    {
        public int Calls { get; private set; }
        public Task<string?> ResolveAsDataUrlAsync(string source, CancellationToken cancellationToken)
        { Calls++; return resolve(); }
    }

    private sealed class SwitchingStream(byte[] bytes, bool seekable, bool yieldRead, Action change) : Stream
    {
        private readonly MemoryStream source = new(bytes, writable: false);
        private bool changed;
        public int Reads { get; private set; }
        private void Before() { Reads++; if (!changed) { changed = true; change(); } }
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => seekable ? source.Length : throw new NotSupportedException();
        public override long Position { get => seekable ? source.Position : throw new NotSupportedException(); set { if (!seekable) throw new NotSupportedException(); source.Position = value; } }
        public override int Read(byte[] buffer, int offset, int count) { Before(); return source.Read(buffer, offset, count); }
        public override int Read(Span<byte> buffer) { Before(); return source.Read(buffer); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (yieldRead) await Task.Yield();
            Before();
            return await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => seekable ? source.Seek(offset, origin) : throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
