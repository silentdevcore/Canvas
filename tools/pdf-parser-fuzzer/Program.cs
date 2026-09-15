using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PXA.Importer;
using PXA.Importer.Trust;
using PXA.Pdf;

const int seed = 0x504841;
const int maximumMutations = 48;
const int maximumInputBytes = 256 * 1024;
const int childTimeoutMilliseconds = 2_000;

if (args.Length >= 2 && string.Equals(args[0], "--child", StringComparison.Ordinal))
    return await RunChildAsync(args[1]);

var reportPath = ReadOption(args, "--report") ?? "artifacts/pdf-fuzz/parser-fuzz-report.json";
var source = CreateMinimalPdf();
if (source.Length is < 1 or > maximumInputBytes)
    throw new InvalidOperationException("The deterministic seed is outside the configured input limit.");

var mutations = CreateMutations(source)
    .Take(maximumMutations)
    .Where(mutation => mutation.Bytes.Length is >= 1 and <= maximumInputBytes)
    .ToArray();
var results = new List<MutationResult>(mutations.Length);
var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"pxa-pdf-fuzz-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryDirectory);

try
{
    for (var index = 0; index < mutations.Length; index++)
    {
        var mutation = mutations[index];
        var inputPath = Path.Combine(temporaryDirectory, $"case-{index:D3}.pdf");
        await File.WriteAllBytesAsync(inputPath, mutation.Bytes);
        results.Add(await RunIsolatedCaseAsync(inputPath, mutation, childTimeoutMilliseconds));
    }
}
finally
{
    if (Directory.Exists(temporaryDirectory))
        Directory.Delete(temporaryDirectory, recursive: true);
}

var report = new FuzzReport(
    SchemaVersion: 1,
    Seed: seed,
    MaximumMutations: maximumMutations,
    MaximumInputBytes: maximumInputBytes,
    ChildTimeoutMilliseconds: childTimeoutMilliseconds,
    MutationCount: results.Count,
    Cases: results);
var fullReportPath = Path.GetFullPath(reportPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
await File.WriteAllTextAsync(
    fullReportPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

Console.WriteLine($"Parser fuzz report: {fullReportPath}");
Console.WriteLine($"Cases: {results.Count}; crashes: {results.Count(result => result.Status == "crashed")}; timeouts: {results.Count(result => result.Status == "timeout")}");
return results.Any(result => result.Status is "crashed" or "timeout") ? 1 : 0;

static async Task<MutationResult> RunIsolatedCaseAsync(
    string inputPath,
    Mutation mutation,
    int timeoutMilliseconds)
{
    var stopwatch = Stopwatch.StartNew();
    using var process = new Process { StartInfo = CreateChildStartInfo(inputPath) };
    if (!process.Start())
        return mutation.ToResult("crashed", null, stopwatch.ElapsedMilliseconds);

    using var timeout = new CancellationTokenSource(timeoutMilliseconds);
    try
    {
        await process.WaitForExitAsync(timeout.Token);
        stopwatch.Stop();
        return mutation.ToResult(
            process.ExitCode == 0 ? "completed" : "crashed",
            process.ExitCode,
            stopwatch.ElapsedMilliseconds);
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

        return mutation.ToResult("timeout", null, stopwatch.ElapsedMilliseconds);
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

        return mutation.ToResult("crashed", null, stopwatch.ElapsedMilliseconds);
    }
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
    public MutationResult ToResult(string status, int? exitCode, long durationMilliseconds) =>
        new(Id, Convert.ToHexStringLower(SHA256.HashData(Bytes)), status, exitCode, durationMilliseconds);
}

sealed record MutationResult(
    string Id,
    string Sha256,
    string Status,
    int? ExitCode,
    long DurationMilliseconds);

sealed record FuzzReport(
    int SchemaVersion,
    int Seed,
    int MaximumMutations,
    int MaximumInputBytes,
    int ChildTimeoutMilliseconds,
    int MutationCount,
    IReadOnlyList<MutationResult> Cases);
