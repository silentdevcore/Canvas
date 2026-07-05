using System.Text.Json;
using PXA.Core.Contracts;

namespace PXA.Infrastructure.Spreadsheet;

internal static class SheetAdapters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Canvas.Core.Contracts.SheetDto ToCanvasSheet(this SheetDto sheet) =>
        Convert<SheetDto, Canvas.Core.Contracts.SheetDto>(sheet);

    public static SheetDto ToPxaSheet(this Canvas.Core.Contracts.SheetDto sheet) =>
        Convert<Canvas.Core.Contracts.SheetDto, SheetDto>(sheet);

    private static TTarget Convert<TSource, TTarget>(TSource source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<TTarget>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to convert {typeof(TSource).Name} to {typeof(TTarget).Name}.");
    }
}
