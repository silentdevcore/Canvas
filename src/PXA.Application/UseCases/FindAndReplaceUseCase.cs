using PXA.Core.Contracts;
using CanvasUseCases = Canvas.Application.UseCases;

namespace PXA.Application.UseCases;

public sealed class FindAndReplaceRequest
{
    public required DesignExportDto Design { get; set; }
    public required string Find { get; set; }
    public required string Replace { get; set; }
    public bool CaseSensitive { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
}

public sealed class FindAndReplaceResult
{
    public required DesignExportDto Design { get; set; }
    public int ReplacementCount { get; set; }
    public List<string> AffectedElementIds { get; set; } = [];
}

/// <summary>
/// PXA-facing facade for text replacement across a design document.
/// </summary>
public sealed class FindAndReplaceUseCase
{
    private readonly CanvasUseCases.FindAndReplaceUseCase inner = new();

    public FindAndReplaceResult Execute(FindAndReplaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = inner.Execute(new CanvasUseCases.FindAndReplaceRequest
        {
            Design = request.Design.ToCanvas(),
            Find = request.Find,
            Replace = request.Replace,
            CaseSensitive = request.CaseSensitive,
            WholeWord = request.WholeWord,
            UseRegex = request.UseRegex,
        });

        return new FindAndReplaceResult
        {
            Design = result.Design.ToPxa(),
            ReplacementCount = result.ReplacementCount,
            AffectedElementIds = result.AffectedElementIds,
        };
    }
}
