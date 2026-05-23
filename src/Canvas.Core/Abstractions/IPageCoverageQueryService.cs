namespace Canvas.Core.Abstractions;

public interface IPageCoverageQueryService
{
    IReadOnlyList<int> GetPagesWithText(object documentModel);

    IReadOnlyList<int> GetPagesWithImages(object documentModel);

    IReadOnlyList<int> GetPagesWithLinks(object documentModel);

    IReadOnlyList<int> GetPagesWithShapes(object documentModel);
}
