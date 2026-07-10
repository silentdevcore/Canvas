using PXA.Core.Contracts;

namespace PXA.Application.UseCases;

public record ExportDocumentRequest(DesignExportDto Design, string Format, ExportOptions? Options = null);

public record ExportResult(byte[] Data, string MimeType, string FileName);
