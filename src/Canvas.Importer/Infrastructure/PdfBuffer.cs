using System.Buffers;

namespace Canvas.Importer.Infrastructure;

public sealed class PdfBuffer : IDisposable
{
    private readonly byte[] _buffer;

    private PdfBuffer(byte[] buffer, int length)
    {
        _buffer = buffer;
        Memory = buffer.AsMemory(0, length);
    }

    public ReadOnlyMemory<byte> Memory { get; }

    public static async Task<PdfBuffer> FromStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            var length = checked((int)stream.Length);
            var rented = ArrayPool<byte>.Shared.Rent(length);
            var offset = 0;
            while (offset < length)
            {
                var read = await stream.ReadAsync(rented.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            return new PdfBuffer(rented, offset);
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        return new PdfBuffer(bytes, bytes.Length);
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}
