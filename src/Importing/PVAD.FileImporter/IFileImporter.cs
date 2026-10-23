using PVAD.Core.Contracts;

namespace PVAD.FileImporter;

/// <summary>
/// Converts a source file stream into a PVADE design document.
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

    /// <summary>
    /// Imports the supplied file stream and observes request cancellation where supported.
    /// </summary>
    Task<DesignExportDto> ImportAsync(
        Stream stream,
        string? name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ImportAsync(stream, name);
    }
}
