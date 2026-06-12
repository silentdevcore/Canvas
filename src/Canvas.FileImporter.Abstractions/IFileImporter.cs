using Canvas.Core.Contracts;

namespace Canvas.FileImporter.Abstractions;

/// <summary>
/// Converts a file stream into a <see cref="DesignExportDto"/> Canvas design.
/// Each implementation handles exactly one file format.
/// </summary>
public interface IFileImporter
{
    /// <summary>Lowercase extensions without a leading dot, e.g. ["docx"].</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    Task<DesignExportDto> ImportAsync(Stream stream, string? name = null);
}
