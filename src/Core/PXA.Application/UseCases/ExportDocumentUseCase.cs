using PXA.Core.Abstractions;

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
        var safeName = MakeSafeFileName(request.Design.Name);
        var fileName = $"{safeName}{exporter.FileExtension}";

        return new ExportResult(data, exporter.MimeType, fileName);
    }

    public IEnumerable<ExporterInfo> GetSupportedFormats()
        => _exporters.Values.Select(e => new ExporterInfo(
            e.FormatKey, e.MimeType, e.FileExtension, e.Capabilities));

    private static string MakeSafeFileName(string name)
    {
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(safe) ? "export" : safe.Trim();
    }
}

public record ExporterInfo(string Key, string MimeType, string Extension, IExporterCapabilities Capabilities);
