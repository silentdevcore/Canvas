using System.Text.Json;

namespace PXA.Core.Contracts;

/// <summary>
/// JSON-compatible adapters between PXA contracts and the legacy Canvas contracts.
/// </summary>
public static class ContractAdapters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Canvas.Core.Contracts.DesignExportDto ToCanvas(this DesignExportDto design) =>
        Convert<DesignExportDto, Canvas.Core.Contracts.DesignExportDto>(design);

    public static DesignExportDto ToPxa(this Canvas.Core.Contracts.DesignExportDto design) =>
        Convert<Canvas.Core.Contracts.DesignExportDto, DesignExportDto>(design);

    public static Canvas.Core.Contracts.SpreadsheetDto ToCanvas(this SpreadsheetDto workbook) =>
        Convert<SpreadsheetDto, Canvas.Core.Contracts.SpreadsheetDto>(workbook);

    public static SpreadsheetDto ToPxa(this Canvas.Core.Contracts.SpreadsheetDto workbook) =>
        Convert<Canvas.Core.Contracts.SpreadsheetDto, SpreadsheetDto>(workbook);

    public static Canvas.Core.Contracts.ExportOptions ToCanvas(this ExportOptions options) => new(
        options.Dpi,
        options.Quality,
        options.CancellationToken,
        options.WordFidelityV2);

    public static ExportOptions ToPxa(this Canvas.Core.Contracts.ExportOptions options) => new(
        options.Dpi,
        options.Quality,
        options.CancellationToken,
        options.WordFidelityV2);

    private static TTarget Convert<TSource, TTarget>(TSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<TTarget>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to convert {typeof(TSource).Name} to {typeof(TTarget).Name}.");
    }
}
