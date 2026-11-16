using PVAD.Importer.Infrastructure;
using PVAD.Importer.Objects;

namespace PVAD.Importer.Tests;

public sealed class PdfBufferTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Preserves_bytes_and_allows_repeated_disposal_for_both_stream_kinds(bool seekable)
    {
        var bytes = Enumerable.Range(0, 137).Select(i => (byte)i).ToArray();
        using var source = new ReadStream(bytes, seekable);
        var buffer = await PdfBuffer.FromStreamAsync(source, default);
        Assert.Equal(bytes, buffer.Memory.ToArray());
        buffer.Dispose();
        buffer.Dispose();
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task Cancelled_admission_does_not_touch_the_stream()
    {
        using var source = new ReadStream([1, 2, 3], true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfBuffer.FromStreamAsync(source, cancellation.Token));
        Assert.Equal(0, source.Reads);
    }

    [Fact]
    public async Task Read_failures_preserve_the_original_exception_and_leave_input_open()
    {
        using var source = new ReadStream([1, 2, 3], true, fail: true);
        await Assert.ThrowsAsync<IOException>(() => PdfBuffer.FromStreamAsync(source, default));
        Assert.True(source.CanRead);
        using var recovered = await PdfBuffer.FromStreamAsync(new MemoryStream([4, 5, 6]), default);
        Assert.Equal(new byte[] { 4, 5, 6 }, recovered.Memory.ToArray());
    }

    [Fact]
    public async Task Loaded_document_streams_do_not_alias_a_recycled_input_buffer()
    {
#pragma warning disable PVAD0001 // Synthetic PDF input for the buffer-lifetime regression.
        var document = new PVAD.Pdf.PdfDocument();
#pragma warning restore PVAD0001
        document.AddPage().DrawText("Owned input data", 30, 40);
        var bytes = document.ToBytes();
        using var input = new MemoryStream(bytes);
        var loaded = await new PdfImporter().LoadAsync(input);
        var streams = loaded.ObjectGraph.Objects.Values.Select(o => o.Value).OfType<PdfStreamObject>()
            .Select(s => (Stream: s, Expected: s.EncodedBytes.ToArray())).ToArray();
        Assert.NotEmpty(streams);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            using var replacementInput = new MemoryStream(Enumerable.Repeat((byte)0xcc, bytes.Length).ToArray());
            using var replacement = await PdfBuffer.FromStreamAsync(replacementInput, default);
            foreach (var (stream, expected) in streams) Assert.Equal(expected, stream.EncodedBytes.ToArray());
        }
    }

    private sealed class ReadStream(byte[] bytes, bool seekable, bool fail = false) : Stream
    {
        private readonly MemoryStream source = new(bytes);
        public int Reads { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => source.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            if (fail) throw new IOException("Synthetic read failure.");
            return source.ReadAsync(buffer, cancellationToken);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
