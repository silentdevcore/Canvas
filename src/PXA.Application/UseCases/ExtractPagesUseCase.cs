using PXA.Core.Contracts;
using CanvasUseCases = Canvas.Application.UseCases;

namespace PXA.Application.UseCases;

public sealed class ExtractPagesRequest
{
    public required DesignExportDto Design { get; set; }
    public required IReadOnlyList<int> PageNumbers { get; set; }
    public string? NewName { get; set; }
}

/// <summary>
/// PXA-facing facade for extracting selected pages from a design document.
/// </summary>
public sealed class ExtractPagesUseCase
{
    private readonly CanvasUseCases.ExtractPagesUseCase inner = new();

    public DesignExportDto Execute(ExtractPagesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = inner.Execute(new CanvasUseCases.ExtractPagesRequest
        {
            Design = request.Design.ToCanvas(),
            PageNumbers = request.PageNumbers,
            NewName = request.NewName,
        });

        return result.ToPxa();
    }
}
