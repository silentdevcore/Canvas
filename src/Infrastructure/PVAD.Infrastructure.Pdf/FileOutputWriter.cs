using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Writes already-produced bytes to a local file; it does not render or validate PDF content.</summary>
/// <remarks>This byte sink does not grant or check native processing rights. Rendering must pass
/// the license checks of the renderer or engine that produced the bytes.</remarks>
public sealed class FileOutputWriter : IOutputWriter
{
    /// <summary>Creates or overwrites a file with the supplied bytes.</summary>
    /// <param name="path">A nonblank local destination path.</param>
    /// <param name="data">The complete byte payload; not null.</param>
    /// <exception cref="ArgumentException">The path is blank.</exception>
    /// <exception cref="ArgumentNullException">The byte array is null.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public void Write(string path, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(data);

        File.WriteAllBytes(path, data);
    }
}
