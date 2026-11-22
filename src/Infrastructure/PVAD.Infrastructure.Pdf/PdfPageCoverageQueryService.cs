using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Inspects existing in-memory PDF elements, without parsing external PDFs or performing OCR.</summary>
/// <remarks>Coverage queries do not start a new licensed rendering operation.</remarks>
public sealed class PdfPageCoverageQueryService : IPageCoverageQueryService
{
    /// <summary>Finds native text elements.</summary>
    /// <param name="documentModel">A native PDF document.</param>
    /// <returns>One-based matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithText(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithText();
    }

    /// <summary>Finds native image elements.</summary>
    /// <param name="documentModel">A native PDF document.</param>
    /// <returns>One-based matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithImages(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithImages();
    }

    /// <summary>Finds native link annotations.</summary>
    /// <param name="documentModel">A native PDF document.</param>
    /// <returns>One-based matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithLinks(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithLinks();
    }

    /// <summary>Finds native shape elements.</summary>
    /// <param name="documentModel">A native PDF document.</param>
    /// <returns>One-based matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithShapes(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithShapes();
    }

    private static PVAD.Pdf.PdfDocument RequirePdfDocument(object documentModel)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfPageCoverageQueryService.", nameof(documentModel));
        }

        return document;
    }
}
