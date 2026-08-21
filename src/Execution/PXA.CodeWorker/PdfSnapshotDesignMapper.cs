using PXA.Core.Contracts;
using PXA.Pdf;

namespace PXA.CodeWorker;

internal static class PdfSnapshotDesignMapper
{
    public static DesignExportDto Map(PdfDesignSnapshot snapshot, string source, int maximumElements,
        List<PxaCodeDiagnosticDto> diagnostics, List<PxaCodeSourceMapEntryDto> sourceMap)
    {
        var elementIds = ParseElementIds(source);
        var design = new DesignExportDto { Id = "code-design", Name = "Generated from C# PDF" };
        var sequence = 0;
        foreach (var (page, pageIndex) in snapshot.Pages.Select((value, index) => (value, index)))
        {
            var outputPage = new PageDto { Id = $"page-{pageIndex + 1}" };
            foreach (var element in page.Elements)
            {
                if (++sequence > maximumElements)
                    throw new InvalidOperationException($"PXACODE021: The result exceeds {maximumElements} elements.");
                var mapped = MapElement(element, page.Height, elementIds.ElementAtOrDefault(sequence - 1)?.Id ?? $"el-{pageIndex}-{element.OperationIndex}");
                if (mapped is null)
                {
                    diagnostics.Add(new PxaCodeDiagnosticDto
                    {
                        Code = "PXACODE022", Severity = "warning",
                        Message = $"The PDF operation '{element.Kind}' is preserved in the PDF but cannot be edited visually.",
                    });
                    continue;
                }
                outputPage.Elements.Add(mapped);
                var sourceId = elementIds.ElementAtOrDefault(sequence - 1);
                if (sourceId is not null)
                    sourceMap.Add(new PxaCodeSourceMapEntryDto
                    {
                        ElementId = mapped.Id, Language = PxaCodeLanguages.CSharpPdf,
                        StartLine = sourceId.Line, StartColumn = 1, EndLine = sourceId.Line, EndColumn = 1,
                    });
            }
            design.Pages.Add(outputPage);
            if (pageIndex == 0)
                design.PageSettings = new PageSettingsDto { Width = page.Width, Height = page.Height };
        }
        return design;
    }

    private static ElementDto? MapElement(PdfDesignElementSnapshot value, double pageHeight, string id)
    {
        var y = pageHeight - value.Y - value.Height;
        var style = new Dictionary<string, object>();
        if (value.FillColor is not null) style["backgroundColor"] = value.FillColor;
        if (value.StrokeColor is not null) style[value.Kind == "text" ? "color" : "borderColor"] = value.StrokeColor;
        if (value.StrokeWidth > 0) style["borderWidth"] = value.StrokeWidth;
        if (value.FontSize > 0) style["fontSize"] = value.FontSize;
        if (value.CornerRadius > 0) style["borderRadius"] = value.CornerRadius;
        return value.Kind switch
        {
            "text" => New("text", value.X, pageHeight - value.Y - value.FontSize, value.Width, value.Height, value.Text, style, id,
                value.Language, value.TextDirection),
            "rectangle" => New("rect", value.X, y, value.Width, value.Height, null, style, id),
            "line" => New("line", value.X, pageHeight - value.Y, value.Width, value.Height, null, style, id),
            "circle" => New("circle", value.X, y, value.Width, value.Height, null, style, id),
            "polygon" or "path" => New("shape", value.X, y, value.Width, value.Height, null, style, id),
            "image" => New("image", value.X, y, value.Width, value.Height, null,
                new Dictionary<string, object> { ["codeWorkerAssetRequired"] = true }, id),
            _ => null,
        };
    }

    private static ElementDto New(string type, double x, double y, double width, double height,
        string? content, Dictionary<string, object> style, string id, string? language = null, string? direction = null) => new()
    {
        Id = id, Type = type, X = x, Y = y, Width = Math.Max(width, 1), Height = Math.Max(height, 1),
        Content = content, Style = style, Language = language, TextDirection = direction,
    };

    private static List<SourceElementId> ParseElementIds(string source)
    {
        var result = new List<SourceElementId>();
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            const string marker = "pxa-element-id:";
            var markerIndex = lines[index].IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) continue;
            var id = lines[index][(markerIndex + marker.Length)..].Trim();
            if (id.Length is > 0 and <= 200)
                result.Add(new SourceElementId(id, index + 1));
        }
        return result;
    }

    private sealed record SourceElementId(string Id, int Line);
}
