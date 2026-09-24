using PXA.Importer.Saving;
using PXA.Importer.Security;

namespace PXA.Importer.Editing;

public enum PdfPageOperationStatus
{
    Success = 0,
    Failed = 1
}

public enum PdfEditablePageSize
{
    A4,
    A3,
    Letter
}

public enum PdfEditableMergeOptions
{
    Default,
    ExcludeAttachments
}

/// <summary>
/// Stateful, migration-friendly facade for bounded existing-PDF page operations. Methods
/// return a status so converted SDK control flow can remain intact; detailed failures are
/// available through <see cref="LastError"/>.
/// </summary>
public sealed class PdfEditableDocument : IDisposable
{
    private readonly PdfPageComposer _composer = new();
    private readonly PdfPageEditor _editor = new();
    private byte[]? _bytes;
    private PdfDocumentEncryptionOptions? _encryption;

    public Exception? LastError { get; private set; }
    public bool IsLoaded => _bytes is { Length: > 0 };

    public PdfPageOperationStatus NewDocument() => Execute(() =>
    {
        _bytes = null;
        _encryption = null;
    });

    public PdfPageOperationStatus LoadFromFile(string path, bool loadInMemory = true) =>
        Execute(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            _bytes = File.ReadAllBytes(path);
            ValidateLoadedBytes();
            _ = loadInMemory;
        });

    public PdfPageOperationStatus LoadFromStream(Stream stream, bool ownStream = false) =>
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(stream);
            try
            {
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                _bytes = buffer.ToArray();
                ValidateLoadedBytes();
            }
            finally
            {
                if (ownStream)
                    stream.Dispose();
            }
        });

    public PdfPageOperationStatus MovePage(int pageNumber, int targetPageNumber) =>
        Execute(() => _bytes = _editor.Apply(RequireBytes(),
            [new PdfMovePageOperation(pageNumber, targetPageNumber)]).Bytes);

    public PdfPageOperationStatus DeletePage(int pageNumber) =>
        Execute(() => _bytes = _editor.Apply(RequireBytes(),
            [new PdfDeletePageOperation(pageNumber)]).Bytes);

    public PdfPageOperationStatus ClonePage(PdfEditableDocument source, int pageNumber) =>
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(source);
            var sourceBytes = source.RequireBytes().ToArray();
            if (_bytes is null)
            {
#pragma warning disable PXA0001
                var blank = new PXA.Pdf.PdfDocument();
#pragma warning restore PXA0001
                blank.AddPage(1, 1);
                var withPage = _composer.Apply(blank.ToBytes(), [new PdfPageImport(sourceBytes, pageNumber)]).Bytes;
                _bytes = _editor.Apply(withPage, [new PdfDeletePageOperation(1)]).Bytes;
            }
            else
            {
                _bytes = _composer.Apply(_bytes, [new PdfPageImport(sourceBytes, pageNumber)]).Bytes;
            }
        });

    public PdfPageOperationStatus ClonePage(int pageNumber) => ClonePage(this, pageNumber);

    public PdfPageOperationStatus InsertBlankPage(double width, double height, int pageNumber) =>
        Execute(() =>
        {
            if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(height) || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Page dimensions must be finite and positive.");
#pragma warning disable PXA0001
            var blank = new PXA.Pdf.PdfDocument();
#pragma warning restore PXA0001
            blank.AddPage(width, height);
            var blankBytes = blank.ToBytes();
            _bytes = _bytes is null
                ? pageNumber == 1
                    ? blankBytes
                    : throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "The first blank page must be inserted at position 1.")
                : _composer.Apply(_bytes, [new PdfPageImport(blankBytes, 1, pageNumber)]).Bytes;
        });

    public PdfPageOperationStatus InsertPage(PdfEditablePageSize size, int pageNumber)
    {
        var dimensions = size switch
        {
            PdfEditablePageSize.A4 => (Width: 595d, Height: 842d),
            PdfEditablePageSize.A3 => (Width: 842d, Height: 1191d),
            PdfEditablePageSize.Letter => (Width: 612d, Height: 792d),
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown page size.")
        };
        return InsertBlankPage(dimensions.Width, dimensions.Height, pageNumber);
    }

    public PdfPageOperationStatus InsertPage(double width, double height, int pageNumber) =>
        InsertBlankPage(width, height, pageNumber);

    public PdfEditableDocument MergeDocuments(params PdfEditableDocument[] documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var merged = new PdfEditableDocument();
        try
        {
            merged._bytes = _composer.Merge(documents.Select(static document =>
            {
                ArgumentNullException.ThrowIfNull(document);
                return (ReadOnlyMemory<byte>)document.RequireBytes();
            }).ToArray()).Bytes;
            merged.LastError = null;
        }
        catch (Exception error)
        {
            merged.LastError = error;
        }
        return merged;
    }

    public PdfEditableDocument MergeDocuments(
        PdfEditableDocument[] documents,
        PdfEditableMergeOptions options)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var result = new PdfEditableDocument();
        result.LastError = new NotSupportedException(
            $"Merge option '{options}' requires catalog-level attachment handling and must be reviewed before migration.");
        return result;
    }

    public PdfPageOperationStatus MergeDocuments(string[] sourcePaths, string destinationPath) =>
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(sourcePaths);
            if (sourcePaths.Length == 0)
                throw new ArgumentException("At least one source path is required.", nameof(sourcePaths));
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
            var documents = sourcePaths.Select(File.ReadAllBytes)
                .Select(static bytes => (ReadOnlyMemory<byte>)bytes)
                .ToArray();
            File.WriteAllBytes(destinationPath, _composer.Merge(documents).Bytes);
        });

    public PdfEditableDocument Merge2Documents(PdfEditableDocument first, PdfEditableDocument second) =>
        MergeDocuments(first, second);

    public PdfPageOperationStatus Merge2Documents(string firstPath, string secondPath, string destinationPath) =>
        Execute(() =>
        {
            var merged = _composer.Merge([File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath)]).Bytes;
            File.WriteAllBytes(destinationPath, merged);
        });

    public PdfPageOperationStatus SaveToFile(string path) => Execute(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = RequireBytes();
        if (_encryption is not null)
        {
            bytes = new PdfFullRewriteWriter().Rewrite(bytes, new PdfFullRewriteOptions
            {
                Encryption = _encryption
            }).Bytes;
        }
        File.WriteAllBytes(path, bytes);
    });

    public PdfPageOperationStatus ConfigureAes256(string ownerPassword, string userPassword) =>
        Execute(() => _encryption = new PdfDocumentEncryptionOptions
        {
            OwnerPassword = ownerPassword,
            UserPassword = userPassword,
            Algorithm = PdfDocumentEncryptionAlgorithm.Aes256
        });

    public int GetPageCount() => _composer.GetPageCount(RequireBytes());

    public PdfPageOperationStatus GetStatus() => LastError is null
        ? PdfPageOperationStatus.Success
        : PdfPageOperationStatus.Failed;

    public byte[] ToBytes() => RequireBytes().ToArray();

    public void Dispose()
    {
        _bytes = null;
        _encryption = null;
        LastError = null;
    }

    private PdfPageOperationStatus Execute(Action action)
    {
        try
        {
            action();
            LastError = null;
            return PdfPageOperationStatus.Success;
        }
        catch (Exception error)
        {
            LastError = error;
            return PdfPageOperationStatus.Failed;
        }
    }

    private byte[] RequireBytes() => _bytes is { Length: > 0 }
        ? _bytes
        : throw new InvalidOperationException("No PDF document is loaded.");

    private void ValidateLoadedBytes() => _ = _composer.GetPageCount(RequireBytes());
}
