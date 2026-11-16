using PVAD.Importer.Saving;
using PVAD.Importer.Security;
using PVAD.Licensing;

namespace PVAD.Importer.Editing;

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

public enum PdfEditablePageBox
{
    CropBox
}

public enum PdfEditableMeasurementUnit
{
    Point,
    Millimeter,
    Centimeter,
    Inch
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
    private int _currentPageNumber = 1;
    private double _measurementScale = 1;

    public Exception? LastError { get; private set; }
    public bool IsLoaded => _bytes is { Length: > 0 };

    public PdfPageOperationStatus NewDocument() => Execute(() =>
    {
        _bytes = null;
        _encryption = null;
        _currentPageNumber = 1;
        _measurementScale = 1;
    });

    public PdfPageOperationStatus LoadFromFile(string path, bool loadInMemory = true) =>
        Execute(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var candidate = File.ReadAllBytes(path);
            _ = _composer.GetPageCountAdmitted(candidate);
            _bytes = candidate;
            _currentPageNumber = 1;
            _measurementScale = 1;
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
                var candidate = buffer.ToArray();
                _ = _composer.GetPageCountAdmitted(candidate);
                _bytes = candidate;
                _currentPageNumber = 1;
                _measurementScale = 1;
            }
            finally
            {
                if (ownStream)
                    stream.Dispose();
            }
        });

    public PdfPageOperationStatus MovePage(int pageNumber, int targetPageNumber) =>
        Execute(() => _bytes = _editor.ApplyAdmitted(RequireBytes(),
            [new PdfMovePageOperation(pageNumber, targetPageNumber)]).Bytes);

    public PdfPageOperationStatus DeletePage(int pageNumber) =>
        Execute(() => _bytes = _editor.ApplyAdmitted(RequireBytes(),
            [new PdfDeletePageOperation(pageNumber)]).Bytes);

    public PdfPageOperationStatus SelectPage(int pageNumber) =>
        Execute(() =>
        {
            var count = _composer.GetPageCountAdmitted(RequireBytes());
            if (pageNumber < 1 || pageNumber > count)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, $"Page number must be between 1 and {count}.");
            _currentPageNumber = pageNumber;
        });

    public PdfPageOperationStatus SetMeasurementUnit(PdfEditableMeasurementUnit unit) =>
        Execute(() => _measurementScale = unit switch
        {
            PdfEditableMeasurementUnit.Point => 1d,
            PdfEditableMeasurementUnit.Millimeter => 72d / 25.4d,
            PdfEditableMeasurementUnit.Centimeter => 72d / 2.54d,
            PdfEditableMeasurementUnit.Inch => 72d,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown PDF measurement unit.")
        });

    public PdfPageOperationStatus SetPageBox(
        PdfEditablePageBox pageBox,
        double left,
        double top,
        double right,
        double bottom) =>
        Execute(() => _bytes = _editor.ApplyAdmitted(RequireBytes(),
            [new PdfSetPageBoxOperation(
                _currentPageNumber,
                MapPageBox(pageBox),
                left * _measurementScale,
                top * _measurementScale,
                right * _measurementScale,
                bottom * _measurementScale)]).Bytes);

    public PdfPageOperationStatus RemovePageBox(PdfEditablePageBox pageBox) =>
        Execute(() => _bytes = _editor.ApplyAdmitted(RequireBytes(),
            [new PdfRemovePageBoxOperation(_currentPageNumber, MapPageBox(pageBox))]).Bytes);

    public PdfPageOperationStatus GetPageBox(
        PdfEditablePageBox pageBox,
        ref float left,
        ref float top,
        ref float right,
        ref float bottom)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
        try
        {
            var box = _editor.ReadPageBoxAdmitted(RequireBytes(), _currentPageNumber, MapPageBox(pageBox));
            left = checked((float)(box.Left / _measurementScale));
            top = checked((float)(box.Top / _measurementScale));
            right = checked((float)(box.Right / _measurementScale));
            bottom = checked((float)(box.Bottom / _measurementScale));
            LastError = null;
            return PdfPageOperationStatus.Success;
        }
        catch (Exception error) when (error is not PvadLicenseException)
        {
            left = top = right = bottom = 0;
            LastError = error;
            return PdfPageOperationStatus.Failed;
        }
    }

    public PdfPageOperationStatus ClonePage(PdfEditableDocument source, int pageNumber) =>
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(source);
            var sourceBytes = source.RequireBytes().ToArray();
            if (_bytes is null)
            {
                var blank = PVAD.Pdf.Serialization.PdfWriter.CreateBlankPageForImporterAdmitted(1, 1);
                var withPage = _composer.ApplyAdmitted(blank, [new PdfPageImport(sourceBytes, pageNumber)]).Bytes;
                _bytes = _editor.ApplyAdmitted(withPage, [new PdfDeletePageOperation(1)]).Bytes;
            }
            else
            {
                _bytes = _composer.ApplyAdmitted(_bytes, [new PdfPageImport(sourceBytes, pageNumber)]).Bytes;
            }
        });

    public PdfPageOperationStatus ClonePage(int pageNumber) => ClonePage(this, pageNumber);

    public PdfPageOperationStatus InsertBlankPage(double width, double height, int pageNumber)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
        return InsertBlankPageAdmitted(width, height, pageNumber);
    }

    private PdfPageOperationStatus InsertBlankPageAdmitted(double width, double height, int pageNumber) =>
        ExecuteAdmitted(() =>
        {
            if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(height) || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Page dimensions must be finite and positive.");
            var blankBytes = PVAD.Pdf.Serialization.PdfWriter.CreateBlankPageForImporterAdmitted(width, height);
            _bytes = _bytes is null
                ? pageNumber == 1
                    ? blankBytes
                    : throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "The first blank page must be inserted at position 1.")
                : _composer.ApplyAdmitted(_bytes, [new PdfPageImport(blankBytes, 1, pageNumber)]).Bytes;
        });

    public PdfPageOperationStatus InsertPage(PdfEditablePageSize size, int pageNumber)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
        var dimensions = size switch
        {
            PdfEditablePageSize.A4 => (Width: 595d, Height: 842d),
            PdfEditablePageSize.A3 => (Width: 842d, Height: 1191d),
            PdfEditablePageSize.Letter => (Width: 612d, Height: 792d),
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown page size.")
        };
        return InsertBlankPageAdmitted(dimensions.Width, dimensions.Height, pageNumber);
    }

    public PdfPageOperationStatus InsertPage(double width, double height, int pageNumber) =>
        InsertBlankPage(width, height, pageNumber);

    /// <summary>
    /// Clones the selected one-based Nutrient page range at the end of this document.
    /// Selected pages are remapped together so references between them remain valid.
    /// </summary>
    public PdfPageOperationStatus ClonePages(PdfEditableDocument source, string pageRange) =>
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(source);
            var sourceBytes = source.RequireBytes();
            var pages = PdfPageRangeParser.Parse(pageRange, _composer.GetPageCountAdmitted(sourceBytes));
            _bytes = _bytes is null
                ? _composer.ExtractPagesAdmitted(sourceBytes, pages)
                : _composer.AppendPagesAdmitted(_bytes, sourceBytes, pages).Bytes;
            _currentPageNumber = _composer.GetPageCountAdmitted(_bytes);
        });

    public PdfEditableDocument MergeDocuments(params PdfEditableDocument[] documents)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
        ArgumentNullException.ThrowIfNull(documents);
        var merged = new PdfEditableDocument();
        try
        {
            merged._bytes = _composer.MergeAdmitted(documents.Select(static document =>
            {
                ArgumentNullException.ThrowIfNull(document);
                return (ReadOnlyMemory<byte>)document.RequireBytes();
            }).ToArray()).Bytes;
            merged.LastError = null;
        }
        catch (Exception error) when (error is not PvadLicenseException)
        {
            merged.LastError = error;
        }
        return merged;
    }

    public PdfEditableDocument MergeDocuments(
        PdfEditableDocument[] documents,
        PdfEditableMergeOptions options)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
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
            File.WriteAllBytes(destinationPath, _composer.MergeAdmitted(documents).Bytes);
        });

    public PdfEditableDocument Merge2Documents(PdfEditableDocument first, PdfEditableDocument second) =>
        MergeDocuments(first, second);

    public PdfPageOperationStatus Merge2Documents(string firstPath, string secondPath, string destinationPath) =>
        Execute(() =>
        {
            var merged = _composer.MergeAdmitted([File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath)]).Bytes;
            File.WriteAllBytes(destinationPath, merged);
        });

    public PdfPageOperationStatus SaveToFile(string path) => Execute(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = RequireBytes();
        if (_encryption is not null)
        {
            bytes = new PdfFullRewriteWriter().RewriteAdmitted(bytes, new PdfFullRewriteOptions
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

    public int GetPageCount()
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
        return _composer.GetPageCountAdmitted(RequireBytes());
    }

    public PdfPageOperationStatus GetStatus() => LastError is null
        ? PdfPageOperationStatus.Success
        : PdfPageOperationStatus.Failed;

    public byte[] ToBytes()
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
        return RequireBytes().ToArray();
    }

    public void Dispose()
    {
        _bytes = null;
        _encryption = null;
        LastError = null;
        _measurementScale = 1;
    }

    private static PdfPageBoxKind MapPageBox(PdfEditablePageBox pageBox) => pageBox switch
    {
        PdfEditablePageBox.CropBox => PdfPageBoxKind.CropBox,
        _ => throw new ArgumentOutOfRangeException(nameof(pageBox), pageBox, "Unknown page box.")
    };

    private PdfPageOperationStatus Execute(Action action)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfEditing);
        return ExecuteAdmitted(action);
    }

    private PdfPageOperationStatus ExecuteAdmitted(Action action)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        try
        {
            action();
            LastError = null;
            return PdfPageOperationStatus.Success;
        }
        catch (Exception error) when (error is not PvadLicenseException)
        {
            LastError = error;
            return PdfPageOperationStatus.Failed;
        }
    }

    private byte[] RequireBytes() => _bytes is { Length: > 0 }
        ? _bytes
        : throw new InvalidOperationException("No PDF document is loaded.");

}
