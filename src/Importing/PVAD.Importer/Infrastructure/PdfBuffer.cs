using System.Buffers;

namespace PVAD.Importer.Infrastructure;

public sealed class PdfBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly bool _pooled;
    private int _disposed;

    private PdfBuffer(byte[] buffer, int length, bool pooled)
    {
        _buffer = buffer;
        Memory = buffer.AsMemory(0, length);
        _pooled = pooled;
    }

    public ReadOnlyMemory<byte> Memory { get; }

    public static async Task<PdfBuffer> FromStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (stream.CanSeek)
        {
            var length = checked((int)stream.Length);
            var rented = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                var offset = 0;
                while (offset < length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = await stream.ReadAsync(rented.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    offset += read;
                }
                return new PdfBuffer(rented, offset, pooled: true);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(rented);
                throw;
            }
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        return new PdfBuffer(bytes, bytes.Length, pooled: false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _pooled)
            ArrayPool<byte>.Shared.Return(_buffer);
    }
}
