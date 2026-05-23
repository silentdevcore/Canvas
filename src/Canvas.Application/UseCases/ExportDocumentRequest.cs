using Canvas.Core.Contracts;

namespace Canvas.Application.UseCases;

public record ExportDocumentRequest(DesignExportDto Design, string Format, ExportOptions? Options = null);

public record ExportResult(byte[] Data, string MimeType, string FileName);
