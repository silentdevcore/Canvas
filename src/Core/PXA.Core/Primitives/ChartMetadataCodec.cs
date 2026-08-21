using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PXA.Core.Contracts;

namespace PXA.Core.Primitives;

public sealed class ChartMetadataEnvelope
{
    public int SchemaVersion { get; set; } = 2;
    public List<ChartMetadataEntry> Charts { get; set; } = [];
}

public sealed class ChartMetadataEntry
{
    public int PageIndex { get; set; }
    public string PageId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public ChartDefinitionDto Definition { get; set; } = new();
    public string Hash { get; set; } = "";
}

public static class ChartMetadataCodec
{
    public const string PdfInfoKey = "PXAChartsV2";
    public const int MaximumCharts = 256;
    public const int MaximumCompressedBytes = 512 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string? Encode(IReadOnlyList<PlannedPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var entries = new List<ChartMetadataEntry>();
        for (var pageIndex = 0; pageIndex < pages.Count && entries.Count < MaximumCharts; pageIndex++)
        {
            var page = pages[pageIndex];
            foreach (var element in page.Elements.Where(static element =>
                         string.Equals(element.Type, "chart", StringComparison.OrdinalIgnoreCase)))
            {
                var definition = ChartDefinitionNormalizer.Normalize(element);
                if (definition.Series.Count == 0)
                    continue;

                entries.Add(new ChartMetadataEntry
                {
                    PageIndex = pageIndex,
                    PageId = page.PageId,
                    ElementId = element.Id,
                    X = element.X,
                    Y = element.Y,
                    Width = element.Width,
                    Height = element.Height,
                    Definition = definition,
                    Hash = ComputeHash(definition)
                });
                if (entries.Count == MaximumCharts)
                    break;
            }
        }

        if (entries.Count == 0)
            return null;

        var json = JsonSerializer.SerializeToUtf8Bytes(new ChartMetadataEnvelope { Charts = entries }, JsonOptions);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(json);
        return output.Length <= MaximumCompressedBytes ? Convert.ToBase64String(output.ToArray()) : null;
    }

    public static bool TryDecode(string? encoded, out ChartMetadataEnvelope envelope)
    {
        envelope = new ChartMetadataEnvelope();
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaximumCompressedBytes * 2)
            return false;

        try
        {
            var compressed = Convert.FromBase64String(encoded);
            if (compressed.Length > MaximumCompressedBytes)
                return false;

            using var input = new MemoryStream(compressed, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = gzip.Read(buffer)) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > MaximumCompressedBytes * 8L)
                    return false;
            }

            var decoded = JsonSerializer.Deserialize<ChartMetadataEnvelope>(output.ToArray(), JsonOptions);
            if (decoded is null || decoded.SchemaVersion != 2 || decoded.Charts.Count > MaximumCharts)
                return false;

            foreach (var entry in decoded.Charts)
            {
                entry.Definition = ChartDefinitionNormalizer.Normalize(new ElementDto { Chart = entry.Definition });
                if (entry.PageIndex < 0 || string.IsNullOrWhiteSpace(entry.ElementId) ||
                    !CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(ComputeHash(entry.Definition)),
                        Encoding.ASCII.GetBytes(entry.Hash ?? "")))
                    return false;
            }

            envelope = decoded;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or JsonException)
        {
            return false;
        }
    }

    public static string ComputeHash(ChartDefinitionDto definition)
    {
        var normalized = ChartDefinitionNormalizer.Normalize(new ElementDto { Chart = definition });
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalized, JsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
