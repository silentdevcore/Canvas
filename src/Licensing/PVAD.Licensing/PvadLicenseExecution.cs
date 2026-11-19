namespace PVAD.Licensing;

internal enum PvadNativeOperation
{
    PdfSerialization,
    ImporterRegeneration,
    ImporterBlankPage,
    ImageAnalysis,
    ImageOcr,
    PdfImport,
    PdfSave,
    PdfAnalysis,
    DesignerPdfImport,
    PdfEditing,
    PdfTrustInspection,
    PdfChangeImpact,
    PdfSigning,
    PdfTimestamp,
    PdfRedaction,
    PdfRedactionAudit,
    PdfColorResolution,
    PdfEInvoice,
    PdfAssociatedFiles,
    PdfArchiveConversion,
    PdfColorConversion,
    PdfTrustPostProcessing,
    SpreadsheetProcessing,
    ConverterExport,
    PdfCodeMigration,
    SpreadsheetCodeMigration,
    PdfViewerMigration,
    PdfDesignerMigration,
    ReportDesignerMigration,
}

internal enum PvadNativeDependency
{
    RegenerationPdf,
    BlankPagePdf,
    OcrResultPdf,
}

/// <summary>
/// Internal continuations only. Public Require never reads this context, so
/// user callbacks and retained handles cannot inherit an admitted operation.
/// </summary>
internal sealed class PvadLicenseExecution : IDisposable
{
    private static readonly AsyncLocal<PvadLicenseExecution?> Slot = new();
    private readonly PvadLicenseExecution? parent;
    private readonly PvadLicenseOperation admission;
    private readonly PvadNativeOperation operation;
    private readonly object authority;
    private int closed;

    private PvadLicenseExecution(PvadNativeOperation operation, PvadLicenseOperation admission, object authority)
    {
        this.operation = operation;
        this.admission = admission;
        this.authority = authority;
        parent = Slot.Value;
        Slot.Value = this;
    }

    internal static string ProductFor(PvadNativeOperation operation) => operation switch
    {
        PvadNativeOperation.PdfSerialization => PvadLicenseProducts.Pdf,
        PvadNativeOperation.SpreadsheetProcessing => PvadLicenseProducts.Spreadsheet,
        PvadNativeOperation.ConverterExport => PvadLicenseProducts.Converter,
        PvadNativeOperation.PdfCodeMigration => PvadLicenseProducts.PdfCodeMigration,
        PvadNativeOperation.SpreadsheetCodeMigration => PvadLicenseProducts.SpreadsheetCodeMigration,
        PvadNativeOperation.PdfViewerMigration => PvadLicenseProducts.PdfViewerMigration,
        PvadNativeOperation.PdfDesignerMigration => PvadLicenseProducts.PdfDesignerMigration,
        PvadNativeOperation.ReportDesignerMigration => PvadLicenseProducts.ReportDesignerMigration,
        PvadNativeOperation.ImporterRegeneration or PvadNativeOperation.ImporterBlankPage or
        PvadNativeOperation.ImageAnalysis or PvadNativeOperation.ImageOcr or
        PvadNativeOperation.PdfImport or PvadNativeOperation.PdfSave or
        PvadNativeOperation.PdfAnalysis or PvadNativeOperation.DesignerPdfImport or
        PvadNativeOperation.PdfEditing or PvadNativeOperation.PdfTrustInspection or
        PvadNativeOperation.PdfChangeImpact or PvadNativeOperation.PdfSigning or
        PvadNativeOperation.PdfTimestamp or PvadNativeOperation.PdfRedaction or
        PvadNativeOperation.PdfRedactionAudit or PvadNativeOperation.PdfColorResolution or
        PvadNativeOperation.PdfEInvoice or PvadNativeOperation.PdfAssociatedFiles or
        PvadNativeOperation.PdfArchiveConversion or PvadNativeOperation.PdfColorConversion or
        PvadNativeOperation.PdfTrustPostProcessing => PvadLicenseProducts.Importer,
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    internal static PvadLicenseExecution Enter(PvadNativeOperation operation, PvadLicenseOperation admission, object authority)
    {
        if (admission.Product != ProductFor(operation) ||
            (PvadLicenseProducts.IsMigration(admission.Product)
                ? string.IsNullOrWhiteSpace(admission.Provider) || admission.Provider == "auto"
                : admission.Provider is not null))
            throw new InvalidOperationException("The admission does not match the native operation.");
        return new(operation, admission, authority);
    }

    internal static PvadLicenseOperation Demand(string product, object? expectedAuthority = null, string? provider = null)
    {
        var current = Current(expectedAuthority);
        if (product != current.admission.Product)
            throw new InvalidOperationException("An internal continuation cannot change its licensed product.");
        if (provider != current.admission.Provider)
            throw new InvalidOperationException("An internal continuation cannot change or omit its licensed provider.");
        return current.admission;
    }

    internal static PvadLicenseExecution EnterNutrientDesignerPdfImport(object expectedAuthority)
    {
        var current = Current(expectedAuthority);
        var admitted = Demand(PvadLicenseProducts.PdfDesignerMigration, expectedAuthority, "nutrient-viewer");
        if (current.operation != PvadNativeOperation.PdfDesignerMigration)
            throw new InvalidOperationException("Only the Nutrient Designer operation may import its source PDF.");
        // A private source-PDF import, not a transferable Importer or PDF grant.
        return Enter(PvadNativeOperation.DesignerPdfImport,
            new PvadLicenseOperation(admitted.LicenseId, PvadLicenseProducts.Importer, null, admitted.IsGracePeriod),
            expectedAuthority);
    }

    internal static PvadLicenseOperation DemandDependency(PvadNativeDependency dependency, object? expectedAuthority = null)
    {
        var current = Current(expectedAuthority);
        var allowed = (current.operation, dependency) switch
        {
            (PvadNativeOperation.ImporterRegeneration, PvadNativeDependency.RegenerationPdf) => true,
            (PvadNativeOperation.ImporterBlankPage, PvadNativeDependency.BlankPagePdf) => true,
            (PvadNativeOperation.PdfEditing, PvadNativeDependency.BlankPagePdf) => true,
            (PvadNativeOperation.ImageOcr, PvadNativeDependency.OcrResultPdf) => true,
            _ => false,
        };
        if (!allowed)
            throw new InvalidOperationException("The native operation does not permit this internal dependency.");
        return current.admission;
    }

    private static PvadLicenseExecution Current(object? expectedAuthority)
    {
        var current = Slot.Value;
        // Descendant tasks share the closed flag: an inherited context is not a reusable grant.
        if (current is null || Volatile.Read(ref current.closed) != 0)
            throw new InvalidOperationException("An active native library operation is required.");
        if (expectedAuthority is not null && !ReferenceEquals(current.authority, expectedAuthority))
            throw new InvalidOperationException("The native operation belongs to a different license authority.");
        return current;
    }

    public void Dispose()
    {
        if (Volatile.Read(ref closed) != 0) return;
        if (!ReferenceEquals(Slot.Value, this))
            throw new InvalidOperationException("Native operations must close in stack order.");
        Interlocked.Exchange(ref closed, 1);
        Slot.Value = parent;
    }
}
