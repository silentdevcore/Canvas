using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfPageCoverageQueryService : IPageCoverageQueryService
{
    public IReadOnlyList<int> GetPagesWithText(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithText();
    }

    public IReadOnlyList<int> GetPagesWithImages(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithImages();
    }

    public IReadOnlyList<int> GetPagesWithLinks(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithLinks();
    }

    public IReadOnlyList<int> GetPagesWithShapes(object documentModel)
    {
        return RequirePdfDocument(documentModel).GetPagesWithShapes();
    }

    private static PXA.Pdf.PdfDocument RequirePdfDocument(object documentModel)
    {
        if (documentModel is not PXA.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PXA.Pdf.PdfDocument for PdfPageCoverageQueryService.", nameof(documentModel));
        }

        return document;
    }
}
