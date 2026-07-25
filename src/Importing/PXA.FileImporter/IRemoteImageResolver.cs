namespace PXA.FileImporter;

/// <summary>
/// Resolves an external image to a self-contained, validated data URL.
/// </summary>
public interface IRemoteImageResolver
{
    Task<string?> ResolveAsDataUrlAsync(string source, CancellationToken cancellationToken);
}
