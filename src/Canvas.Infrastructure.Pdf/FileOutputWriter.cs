using Canvas.Core.Abstractions;

namespace Canvas.Infrastructure.Pdf;

public sealed class FileOutputWriter : IOutputWriter
{
    public void Write(string path, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(data);

        File.WriteAllBytes(path, data);
    }
}
