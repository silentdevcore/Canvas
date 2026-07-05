using PXA.Core.Contracts;

namespace PXA.FileImporter;

/// <summary>
/// Converts a source file stream into a Power Dox Automation design document.
/// </summary>
public interface IFileImporter
{
    /// <summary>
    /// Lowercase extensions without a leading dot, e.g. <c>["docx"]</c>.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Imports the supplied file stream.
    /// </summary>
    Task<DesignExportDto> ImportAsync(Stream stream, string? name = null);
}
