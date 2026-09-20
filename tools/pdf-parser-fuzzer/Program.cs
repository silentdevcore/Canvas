using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PXA.Importer;
using PXA.Importer.Trust;
using PXA.Pdf;
using SharpFuzz;

const int seed = 0x504841;
const int maximumMutations = 96;
const int maximumInputBytes = 256 * 1024;
const int childTimeoutMilliseconds = 2_000;
const int maximumCapturedOutputCharacters = 8 * 1024;

if (args.Length >= 2 && string.Equals(args[0], "--child", StringComparison.Ordinal))
    return await RunChildAsync(args[1]);
if (args.Length >= 1 && string.Equals(args[0], "--afl", StringComparison.Ordinal))
{
    Fuzzer.OutOfProcess.Run(stream => RunCoverageInput(stream).GetAwaiter().GetResult());
    return 0;
}

var reportPath = ReadOption(args, "--report") ?? "artifacts/pdf-fuzz/parser-fuzz-report.json";
var source = CreateMinimalPdf();
if (source.Length is < 1 or > maximumInputBytes)
    throw new InvalidOperationException("The deterministic seed is outside the configured input limit.");

var mutations = CreateMutations(source)
    .Take(maximumMutations)
    .Where(mutation => mutation.Bytes.Length is >= 1 and <= maximumInputBytes)
    .ToArray();
var results = new List<MutationResult>(mutations.Length);
var externalParsers = DiscoverExternalParsers();
var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"pxa-pdf-fuzz-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryDirectory);

try
{
    for (var index = 0; index < mutations.Length; index++)
    {
        var mutation = mutations[index];
        var inputPath = Path.Combine(temporaryDirectory, $"case-{index:D3}.pdf");
        await File.WriteAllBytesAsync(inputPath, mutation.Bytes);
        var pxa = await RunIsolatedCaseAsync(inputPath, childTimeoutMilliseconds);
        var external = new List<ParserOutcome>(externalParsers.Count);
        foreach (var parser in externalParsers)
            external.Add(await RunExternalParserAsync(parser, inputPath, childTimeoutMilliseconds));
        results.Add(mutation.ToResult(pxa, external));
    }
}
finally
{
    if (Directory.Exists(temporaryDirectory))
        Directory.Delete(temporaryDirectory, recursive: true);
}

var report = new FuzzReport(
    SchemaVersion: 2,
    Seed: seed,
    MaximumMutations: maximumMutations,
    MaximumInputBytes: maximumInputBytes,
    ChildTimeoutMilliseconds: childTimeoutMilliseconds,
    MaximumCapturedOutputCharacters: maximumCapturedOutputCharacters,
    Parsers: new[] { "pxa" }.Concat(externalParsers.Select(parser => parser.Id)).ToArray(),
    MutationCount: results.Count,
    Cases: results);
var fullReportPath = Path.GetFullPath(reportPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
await File.WriteAllTextAsync(
    fullReportPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

Console.WriteLine($"Parser fuzz report: {fullReportPath}");
var outcomes = results.SelectMany(result => result.Outcomes);
Console.WriteLine($"Cases: {results.Count}; parsers: {report.Parsers.Count}; crashes: {outcomes.Count(result => result.Status == "crashed")}; timeouts: {outcomes.Count(result => result.Status == "timeout")}; differentials: {results.Count(result => result.Differential)}");
return outcomes.Any(result => result.Status is "crashed" or "timeout") ? 1 : 0;

static async Task<ParserOutcome> RunIsolatedCaseAsync(string inputPath, int timeoutMilliseconds)
{
    var stopwatch = Stopwatch.StartNew();
    using var process = new Process { StartInfo = CreateChildStartInfo(inputPath) };
    if (!process.Start())
        return new ParserOutcome("pxa", "crashed", null, stopwatch.ElapsedMilliseconds, null);

    using var timeout = new CancellationTokenSource(timeoutMilliseconds);
    var outputTask = ReadBoundedAsync(process.StandardOutput, maximumCapturedOutputCharacters);
    var errorTask = ReadBoundedAsync(process.StandardError, maximumCapturedOutputCharacters);
    try
    {
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        _ = await errorTask;
        stopwatch.Stop();
        var status = process.ExitCode == 0 ? ReadChildStatus(output) : "crashed";
        return new ParserOutcome(
            "pxa",
            status,
            process.ExitCode,
            stopwatch.ElapsedMilliseconds,
            null);
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        stopwatch.Stop();
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            // The child may have exited between the checks.
        }

        return new ParserOutcome("pxa", "timeout", null, stopwatch.ElapsedMilliseconds, null);
    }
    catch
    {
        stopwatch.Stop();
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The child may have exited between the checks.
        }

        return new ParserOutcome("pxa", "crashed", null, stopwatch.ElapsedMilliseconds, null);
    }
}

static async Task<ParserOutcome> RunExternalParserAsync(
    ExternalParser parser,
    string inputPath,
    int timeoutMilliseconds)
{
    var stopwatch = Stopwatch.StartNew();
    var startInfo = new ProcessStartInfo
    {
        FileName = parser.Command,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (var argument in parser.Arguments)
        startInfo.ArgumentList.Add(argument == "{input}" ? inputPath : argument);

    using var process = new Process { StartInfo = startInfo };
    if (!process.Start())
        return new ParserOutcome(parser.Id, "crashed", null, stopwatch.ElapsedMilliseconds, null);

    using var timeout = new CancellationTokenSource(timeoutMilliseconds);
    var outputTask = ReadBoundedAsync(process.StandardOutput, maximumCapturedOutputCharacters);
    var errorTask = ReadBoundedAsync(process.StandardError, maximumCapturedOutputCharacters);
    try
    {
        await process.WaitForExitAsync(timeout.Token);
        _ = await outputTask;
        _ = await errorTask;
        stopwatch.Stop();
        var status = process.ExitCode == 0
            ? "accepted"
            : process.ExitCode >= 128 ? "crashed" : "rejected";
        return new ParserOutcome(parser.Id, status, process.ExitCode, stopwatch.ElapsedMilliseconds, null);
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        stopwatch.Stop();
        KillProcess(process);
        return new ParserOutcome(parser.Id, "timeout", null, stopwatch.ElapsedMilliseconds, null);
    }
    catch (Exception exception)
    {
        stopwatch.Stop();
        KillProcess(process);
        return new ParserOutcome(parser.Id, "crashed", null, stopwatch.ElapsedMilliseconds, exception.GetType().Name);
    }
}

static async Task<string> ReadBoundedAsync(StreamReader reader, int maximumCharacters)
{
    var result = new StringBuilder(Math.Min(maximumCharacters, 1024));
    var buffer = new char[1024];
    while (await reader.ReadAsync(buffer) is var read && read > 0)
    {
        var remaining = maximumCharacters - result.Length;
        if (remaining > 0)
            result.Append(buffer, 0, Math.Min(read, remaining));
    }
    return result.ToString();
}

static string ReadChildStatus(string output)
{
    try
    {
        using var document = JsonDocument.Parse(output.Trim());
        return document.RootElement.GetProperty("status").GetString() switch
        {
            "completed" => "accepted",
            "rejected" => "rejected",
            _ => "crashed"
        };
    }
    catch (JsonException)
    {
        return "crashed";
    }
}

static void KillProcess(Process process)
{
    try
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
    }
    catch (InvalidOperationException)
    {
        // The process may have exited between the checks.
    }
}

static IReadOnlyList<ExternalParser> DiscoverExternalParsers()
{
    var candidates = new[]
    {
        new ExternalParser("poppler-pdfinfo", "pdfinfo", ["{input}"]),
        new ExternalParser("poppler-pdftotext", "pdftotext", ["{input}", "-"]),
        new ExternalParser("qpdf", "qpdf", ["--check", "{input}"]),
        new ExternalParser("mupdf", "mutool", ["info", "{input}"])
    };
    return candidates.Where(parser => FindCommand(parser.Command) is not null).ToArray();
}

static string? FindCommand(string command)
{
    var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        var candidate = Path.Combine(directory, command);
        if (File.Exists(candidate))
            return candidate;
    }
    return null;
}

static ProcessStartInfo CreateChildStartInfo(string inputPath)
{
    var currentProcessPath = Environment.ProcessPath ?? "dotnet";
    var currentProcessName = Path.GetFileNameWithoutExtension(currentProcessPath);
    var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
    var startInfo = new ProcessStartInfo
    {
        FileName = string.Equals(currentProcessName, "dotnet", StringComparison.OrdinalIgnoreCase)
            ? hostPath
            : currentProcessPath,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    if (string.Equals(currentProcessName, "dotnet", StringComparison.OrdinalIgnoreCase))
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
    startInfo.ArgumentList.Add("--child");
    startInfo.ArgumentList.Add(inputPath);
    startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    startInfo.Environment["DOTNET_GCHeapHardLimit"] = "10000000";
    return startInfo;
}

static async Task<int> RunChildAsync(string inputPath)
{
    try
    {
        var bytes = await File.ReadAllBytesAsync(inputPath);
        if (bytes.Length is < 1 or > maximumInputBytes)
            return 2;

        var inspection = await new PdfTrustInspector().InspectAsync(
            new MemoryStream(bytes, writable: false),
            new PdfTrustInspectionOptions
            {
                MaximumFileSizeBytes = maximumInputBytes,
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    RevocationMode = PdfCertificateRevocationMode.NoCheck
                }
            });
        if (inspection.Diagnostics.Any(IsParseDiagnostic) && inspection.StructureStatus == PdfTrustStatus.Valid)
            return 3;

        var importer = new PdfImporter(new PdfImporterOptions
        {
            LazyObjectLoading = false,
            DeferredStreamDecoding = true,
            ParsePagesInParallel = false,
            MaxParallelPageParsers = 1
        });
        await importer.LoadAsync(new MemoryStream(bytes, writable: false));
        Console.WriteLine("{\"status\":\"completed\"}");
        return 0;
    }
    catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { status = "rejected", errorType = exception.GetType().Name }));
        return 0;
    }
}

static async Task RunCoverageInput(Stream input)
{
    using var bounded = new MemoryStream();
    var buffer = new byte[8192];
    while (bounded.Length <= maximumInputBytes)
    {
        var read = await input.ReadAsync(buffer);
        if (read == 0)
            break;
        await bounded.WriteAsync(buffer.AsMemory(0, read));
    }
    if (bounded.Length is < 1 or > maximumInputBytes)
        return;

    var bytes = bounded.ToArray();
    try
    {
        var inspection = await new PdfTrustInspector().InspectAsync(
            new MemoryStream(bytes, writable: false),
            new PdfTrustInspectionOptions
            {
                MaximumFileSizeBytes = maximumInputBytes,
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    RevocationMode = PdfCertificateRevocationMode.NoCheck
                }
            });
        if (inspection.Diagnostics.Any(IsParseDiagnostic) && inspection.StructureStatus == PdfTrustStatus.Valid)
            throw new FuzzInvariantException("A parser diagnostic produced a valid structure verdict.");

        var importer = new PdfImporter(new PdfImporterOptions
        {
            LazyObjectLoading = false,
            DeferredStreamDecoding = true,
            ParsePagesInParallel = false,
            MaxParallelPageParsers = 1
        });
        await importer.LoadAsync(new MemoryStream(bytes, writable: false));
    }
    catch (Exception exception) when (IsExpectedMalformedInput(exception))
    {
        // Rejection is an expected result for malformed fuzz input.
    }
}

static bool IsExpectedMalformedInput(Exception exception) => exception is
    InvalidDataException or
    FormatException or
    NotSupportedException or
    ArgumentException or
    CryptographicException or
    PXA.Importer.Parsing.PdfParseLimitExceededException or
    PXA.Importer.Security.PdfPasswordException or
    PXA.Importer.Security.PdfEncryptionIntegrityException;

static bool IsParseDiagnostic(PdfTrustDiagnostic diagnostic) =>
    diagnostic.Code.StartsWith("PXAPDFPARSE", StringComparison.Ordinal) ||
    diagnostic.Code.StartsWith("PXAPDFXREF", StringComparison.Ordinal);

static string? ReadOption(string[] arguments, string option)
{
    var index = Array.IndexOf(arguments, option);
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static byte[] CreateMinimalPdf()
{
    var document = PXA.Generator.Pdf.CreateDocument();
    document.AddPage(240, 160).DrawText("fuzz seed", 24, 24);
    return document.ToBytes();
}

static IEnumerable<Mutation> CreateMutations(byte[] source)
{
    var random = new Random(seed);
    foreach (var offset in new[] { 1, 8, source.Length / 3, source.Length / 2, source.Length - 8 }
        .Where(index => index > 0 && index < source.Length))
        yield return new Mutation($"truncate-{offset}", source[..offset]);

    foreach (var offset in new[] { 0, 1, 5, 8, source.Length / 4, source.Length / 2, source.Length - 1 })
        yield return Flip(source, $"byte-flip-{offset}", offset, random);

    foreach (var replacement in new[]
    {
        (Old: "<<", New: "<"),
        (Old: ">>", New: ">"),
        (Old: "[", New: ""),
        (Old: "]", New: ""),
        (Old: "obj", New: "obx"),
        (Old: "xref", New: "xrefx")
    })
        yield return Replace(source, $"delimiter-{replacement.Old}", replacement.Old, replacement.New);

    yield return Replace(source, "oversized-number", " 240 ", " 999999999999999999999999999999999999 ");
    yield return Replace(source, "oversized-length", "/Length ", "/Length 999999999999999999999999 ");
    yield return Insert(source, "nested-arrays", Math.Min(source.Length, 64), Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("[ ", 96))));
    yield return Insert(source, "nested-dictionaries", Math.Min(source.Length, 64), Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("<</A ", 64))));
    yield return Replace(source, "negative-size", "/Size 5", "/Size -1");
    yield return Replace(source, "invalid-startxref", $"startxref\n{FindStartXref(source)}", "startxref\n999999999");
    yield return Insert(source, "duplicate-shadow-object", FindEofOffset(source), Encoding.ASCII.GetBytes("\n1 0 obj\n<< /Type /Catalog /Pages 999 0 R >>\nendobj\n"));
    yield return Insert(source, "late-javascript-object", FindEofOffset(source), Encoding.ASCII.GetBytes("\n99 0 obj\n<< /S /JavaScript /JS (alert\\(1\\)) >>\nendobj\n"));
    yield return Insert(source, "recursive-reference", FindEofOffset(source), Encoding.ASCII.GetBytes("\n98 0 obj\n98 0 R\nendobj\n"));
    yield return Insert(source, "unterminated-stream", FindEofOffset(source), Encoding.ASCII.GetBytes("\n97 0 obj\n<< /Length 64 >>\nstream\nunterminated"));
    yield return Insert(source, "fake-eof-middle", source.Length / 2, Encoding.ASCII.GetBytes("\n%%EOF\n"));
    yield return Insert(source, "incremental-empty-revision", source.Length, Encoding.ASCII.GetBytes(
        $"\nxref\n0 1\n0000000000 65535 f \ntrailer\n<< /Size 5 /Prev {FindStartXref(source)} >>\nstartxref\n{source.Length}\n%%EOF\n"));
}

static int FindStartXref(byte[] source)
{
    var text = Encoding.Latin1.GetString(source);
    var marker = text.LastIndexOf("startxref\n", StringComparison.Ordinal);
    if (marker < 0)
        return 0;
    marker += "startxref\n".Length;
    var end = text.IndexOf('\n', marker);
    return end > marker && int.TryParse(text[marker..end], out var value) ? value : 0;
}

static int FindEofOffset(byte[] source)
{
    var marker = Encoding.Latin1.GetString(source).LastIndexOf("%%EOF", StringComparison.Ordinal);
    return marker >= 0 ? marker : source.Length;
}

static Mutation Flip(byte[] source, string id, int offset, Random random)
{
    var bytes = source.ToArray();
    var index = Math.Clamp(offset, 0, bytes.Length - 1);
    bytes[index] ^= (byte)(1 << random.Next(0, 8));
    return new Mutation(id, bytes);
}

static Mutation Replace(byte[] source, string id, string oldValue, string newValue)
{
    var text = Encoding.Latin1.GetString(source);
    var index = text.IndexOf(oldValue, StringComparison.Ordinal);
    if (index < 0)
        return new Mutation($"{id}-not-present", source.ToArray());

    text = text[..index] + newValue + text[(index + oldValue.Length)..];
    return new Mutation(id, Encoding.Latin1.GetBytes(text));
}

static Mutation Insert(byte[] source, string id, int offset, byte[] inserted)
{
    var bytes = new byte[source.Length + inserted.Length];
    Buffer.BlockCopy(source, 0, bytes, 0, offset);
    Buffer.BlockCopy(inserted, 0, bytes, offset, inserted.Length);
    Buffer.BlockCopy(source, offset, bytes, offset + inserted.Length, source.Length - offset);
    return new Mutation(id, bytes);
}

sealed record Mutation(string Id, byte[] Bytes)
{
    public MutationResult ToResult(ParserOutcome pxa, IReadOnlyList<ParserOutcome> external)
    {
        var outcomes = new[] { pxa }.Concat(external).ToArray();
        var decisions = outcomes
            .Where(outcome => outcome.Status is "accepted" or "rejected")
            .Select(outcome => outcome.Status)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new MutationResult(
            Id,
            Convert.ToHexStringLower(SHA256.HashData(Bytes)),
            decisions > 1,
            outcomes);
    }
}

sealed record MutationResult(
    string Id,
    string Sha256,
    bool Differential,
    IReadOnlyList<ParserOutcome> Outcomes);

sealed record ParserOutcome(
    string Parser,
    string Status,
    int? ExitCode,
    long DurationMilliseconds,
    string? ErrorType);

sealed record ExternalParser(string Id, string Command, IReadOnlyList<string> Arguments);

sealed class FuzzInvariantException(string message) : Exception(message);

sealed record FuzzReport(
    int SchemaVersion,
    int Seed,
    int MaximumMutations,
    int MaximumInputBytes,
    int ChildTimeoutMilliseconds,
    int MaximumCapturedOutputCharacters,
    IReadOnlyList<string> Parsers,
    int MutationCount,
    IReadOnlyList<MutationResult> Cases);
