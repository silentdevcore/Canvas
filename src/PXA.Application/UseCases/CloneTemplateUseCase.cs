using PXA.Core.Contracts;
using CanvasUseCases = Canvas.Application.UseCases;

namespace PXA.Application.UseCases;

public sealed class CloneDesignRequest
{
    public required DesignExportDto Design { get; set; }
    public string? NewName { get; set; }
}

/// <summary>
/// PXA-facing facade for cloning a design document.
/// </summary>
public sealed class CloneTemplateUseCase
{
    private readonly CanvasUseCases.CloneTemplateUseCase inner = new();

    public DesignExportDto Execute(CloneDesignRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = inner.Execute(new CanvasUseCases.CloneDesignRequest
        {
            Design = request.Design.ToCanvas(),
            NewName = request.NewName,
        });

        return result.ToPxa();
    }
}
