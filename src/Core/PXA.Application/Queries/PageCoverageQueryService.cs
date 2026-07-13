using PXA.Core.Abstractions;

namespace PXA.Application.Queries;

public sealed class PageCoverageQueryService : IPageCoverageQueryService
{
    private readonly IPageCoverageQueryService _pageCoverage;

    public PageCoverageQueryService(IPageCoverageQueryService pageCoverage)
    {
        _pageCoverage = pageCoverage;
    }

    public IReadOnlyList<int> GetPagesWithText(object documentModel)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        return _pageCoverage.GetPagesWithText(documentModel);
    }

    public IReadOnlyList<int> GetPagesWithImages(object documentModel)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        return _pageCoverage.GetPagesWithImages(documentModel);
    }

    public IReadOnlyList<int> GetPagesWithLinks(object documentModel)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        return _pageCoverage.GetPagesWithLinks(documentModel);
    }

    public IReadOnlyList<int> GetPagesWithShapes(object documentModel)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        return _pageCoverage.GetPagesWithShapes(documentModel);
    }
}
