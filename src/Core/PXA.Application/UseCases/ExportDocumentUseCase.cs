using PXA.Core.Abstractions;
using PXA.Core.Primitives;

namespace PXA.Application.UseCases;

public sealed class ExportDocumentUseCase
{
    private readonly IReadOnlyDictionary<string, IDocumentExporter> _exporters;

    public ExportDocumentUseCase(IEnumerable<IDocumentExporter> exporters)
    {
        _exporters = exporters.ToDictionary(e => e.FormatKey, StringComparer.OrdinalIgnoreCase);
    }

    public ExportResult Execute(ExportDocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_exporters.TryGetValue(request.Format, out var exporter))
        {
            var supported = string.Join(", ", _exporters.Keys.Order());
            throw new NotSupportedException(
                $"Export format '{request.Format}' is not supported. Supported formats: {supported}");
        }

        var data     = exporter.Export(request.Design, request.Options);
        var safeName = ExportFileNameSanitizer.Sanitize(request.Design.Name);
        var isPageArchive = request.Design.Pages.Count > 1 &&
            request.Format.EqualsAny("png", "jpeg", "tiff", "svg");
        var fileName = isPageArchive
            ? $"{safeName}-{request.Format.ToLowerInvariant()}-pages.zip"
            : $"{safeName}{exporter.FileExtension}";
        var mimeType = isPageArchive ? "application/zip" : exporter.MimeType;

        return new ExportResult(data, mimeType, fileName);
    }

    public IEnumerable<ExporterInfo> GetSupportedFormats()
        => _exporters.Values.Select(e => new ExporterInfo(
            e.FormatKey, e.MimeType, e.FileExtension, e.Capabilities));

}

file static class ExportFormatExtensions
{
    public static bool EqualsAny(this string value, params string[] candidates) =>
        candidates.Any(candidate => value.Equals(candidate, StringComparison.OrdinalIgnoreCase));
}

public record ExporterInfo(string Key, string MimeType, string Extension, IExporterCapabilities Capabilities);
