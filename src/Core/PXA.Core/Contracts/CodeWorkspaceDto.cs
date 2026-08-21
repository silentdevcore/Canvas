using System.Text.Json;

namespace PXA.Core.Contracts;

public static class PxaCodeLanguages
{
    public const string Json = "json";
    public const string CSharpModel = "csharpModel";
    public const string CSharpPdf = "csharpPdf";
    public const string CSharpBase64 = "csharpBase64";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        Json, CSharpModel, CSharpPdf, CSharpBase64,
    };
}

public static class PxaCodeFidelity
{
    public const string Exact = "exact";
    public const string Compatible = "compatible";
    public const string ReviewRequired = "reviewRequired";
    public const string Unsupported = "unsupported";
}

public static class PxaCodeSourcePreservation
{
    public const string Preserved = "preserved";
    public const string Regenerated = "regenerated";
    public const string StructureLost = "structureLost";
}

public static class PxaCodeLimits
{
    public const int MaximumDesignBytes = 10 * 1024 * 1024;
    public const int MaximumSourceBytes = 32 * 1024 * 1024;
    public const int MaximumWorkerRequestBytes = 40 * 1024 * 1024;
}

public static class PxaCodeValue
{
    public static JsonElement Json(string source)
    {
        using var document = JsonDocument.Parse(source);
        return document.RootElement.Clone();
    }
}

public sealed class PxaCodeDiagnosticDto
{
    public string Code { get; set; } = "PXACODE000";
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = "";
    public int? Line { get; set; }
    public int? Column { get; set; }
    public string? ElementId { get; set; }
}

public sealed class PxaCodeSourceMapEntryDto
{
    public required string ElementId { get; set; }
    public required string Language { get; set; }
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}

public sealed class PxaCodeConversionResultDto
{
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }
    public required string Fidelity { get; set; }
    public required string DocumentFidelity { get; set; }
    public required string SourcePreservation { get; set; }
    public string GeneratedSource { get; set; } = "";
    public DesignExportDto? CanonicalDesign { get; set; }
    public List<PxaCodeDiagnosticDto> Diagnostics { get; set; } = [];
    public List<PxaCodeSourceMapEntryDto> SourceMap { get; set; } = [];
    public required string SourceChecksum { get; set; }
    public required string ResultChecksum { get; set; }
    public required string CanonicalChecksum { get; set; }
}

public sealed class PxaCodeWorkerRequest
{
    public string Language { get; set; } = PxaCodeLanguages.CSharpPdf;
    public string Source { get; set; } = "";
    public string Operation { get; set; } = "convert";
    public int TimeoutSeconds { get; set; } = 15;
    public int MaximumPages { get; set; } = 200;
    public int MaximumElements { get; set; } = 20_000;
}

public sealed class PxaCodeWorkerResponse
{
    public Guid? JobId { get; set; }
    public bool Success { get; set; }
    public DesignExportDto? CanonicalDesign { get; set; }
    public byte[]? PdfBytes { get; set; }
    public List<PxaCodeDiagnosticDto> Diagnostics { get; set; } = [];
    public List<PxaCodeSourceMapEntryDto> SourceMap { get; set; } = [];
    public string Fidelity { get; set; } = PxaCodeFidelity.Unsupported;
    public JsonElement? Metadata { get; set; }
}
