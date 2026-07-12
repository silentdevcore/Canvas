namespace PXA.Pdf;

/// <summary>
/// Access permissions granted to readers of an encrypted PDF (Standard Security Handler).
/// A cleared flag denies the corresponding operation when the document is opened with the
/// user password. Maps to the <c>/P</c> permission bits of PDF 32000-1, Table 22.
/// </summary>
[Flags]
public enum PdfPermissions
{
    /// <summary>No permissions granted.</summary>
    None = 0,

    /// <summary>Print the document (possibly at low resolution). Bit 3.</summary>
    Print = 1 << 0,

    /// <summary>Modify the document contents. Bit 4.</summary>
    Modify = 1 << 1,

    /// <summary>Copy or extract text and graphics. Bit 5.</summary>
    Copy = 1 << 2,

    /// <summary>Add or modify annotations and fill in form fields. Bit 6.</summary>
    AnnotateAndFillForms = 1 << 3,

    /// <summary>Fill in existing form fields (even if <see cref="AnnotateAndFillForms"/> is denied). Bit 9.</summary>
    FillForms = 1 << 4,

    /// <summary>Extract text and graphics for accessibility. Bit 10.</summary>
    ExtractForAccessibility = 1 << 5,

    /// <summary>Assemble the document (insert, rotate, delete pages). Bit 11.</summary>
    Assemble = 1 << 6,

    /// <summary>Print at high resolution. Bit 12.</summary>
    PrintHighResolution = 1 << 7,

    /// <summary>All permissions granted.</summary>
    All = Print | Modify | Copy | AnnotateAndFillForms | FillForms |
          ExtractForAccessibility | Assemble | PrintHighResolution
}
