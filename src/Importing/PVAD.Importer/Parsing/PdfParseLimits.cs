namespace PVAD.Importer.Parsing;

/// <summary>
/// Bounds applied while parsing an existing PDF. Defaults are chosen so that no
/// legitimate document reaches them; a limit that fires on real input is a defect in the
/// limit, not protection.
/// </summary>
public sealed record PdfParseLimits
{
    /// <summary>
    /// Maximum nesting of arrays, dictionaries, and streams. The deepest structures real
    /// documents use are outline and structure trees, which stay far below this.
    /// </summary>
    public int MaxNestingDepth { get; init; } = 64;

    /// <summary>Maximum elements in one array.</summary>
    public int MaxArrayItemCount { get; init; } = 500_000;

    /// <summary>Maximum entries in one dictionary.</summary>
    public int MaxDictionaryEntryCount { get; init; } = 50_000;

    /// <summary>Maximum indirect objects parsed from one document.</summary>
    public int MaxIndirectObjectCount { get; init; } = 2_000_000;

    /// <summary>Maximum entries read from one object stream, applied before allocating from /N.</summary>
    public int MaxObjectStreamEntryCount { get; init; } = 200_000;

    /// <summary>Maximum decoded bytes produced by one stream.</summary>
    public long MaxDecodedStreamBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// Maximum ratio of decoded to encoded bytes. Applied together with
    /// <see cref="MinimumDecodedStreamBytes"/> so that small streams which legitimately
    /// expand by a large factor are not rejected by the ratio alone.
    /// </summary>
    public int MaxDecompressionRatio { get; init; } = 200;

    /// <summary>Decoded size below which the ratio rule does not apply.</summary>
    public long MinimumDecodedStreamBytes { get; init; } = 1024 * 1024;

    /// <summary>Maximum predictor row length, bounding attacker-controlled /Columns and /Colors.</summary>
    public int MaxPredictorRowLength { get; init; } = 1024 * 1024;

    /// <summary>Maximum CCITT image width before row buffers are allocated.</summary>
    public int MaxCcittColumns { get; init; } = 1_000_000;

    /// <summary>Maximum declared CCITT rows.</summary>
    public int MaxCcittRows { get; init; } = 1_000_000;

    /// <summary>Maximum depth of indirect reference resolution.</summary>
    public int MaxReferenceResolutionDepth { get; init; } = 32;

    /// <summary>Maximum pages built from one document.</summary>
    public int MaxPageCount { get; init; } = 100_000;
}

public sealed record PdfParseDiagnostic(string Code, string Message, int? ObjectNumber = null, int? Generation = null);

/// <summary>
/// Signals that a parse limit was reached. Callers recover at object or stream
/// granularity so that one hostile construct degrades to a diagnostic rather than
/// failing the whole document.
/// </summary>
public sealed class PdfParseLimitExceededException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
