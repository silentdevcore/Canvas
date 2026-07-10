using PXA.Core.Contracts;

namespace PXA.Application.UseCases;

public sealed class ExtractPagesRequest
{
    public required DesignExportDto Design { get; set; }
    /// <summary>1-based page numbers to include in the extract.</summary>
    public required IReadOnlyList<int> PageNumbers { get; set; }
    public string? NewName { get; set; }
}

/// <summary>
/// Returns a new <see cref="DesignExportDto"/> containing only the requested pages
/// (1-based). Page settings and shared elements are carried over unchanged.
/// </summary>
public sealed class ExtractPagesUseCase
{
    public DesignExportDto Execute(ExtractPagesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PageNumbers is null || request.PageNumbers.Count == 0)
            throw new ArgumentException("At least one page number must be specified.", nameof(request));

        var src   = request.Design;
        var total = src.Pages.Count;

        var selected = request.PageNumbers
            .Distinct()
            .Order()
            .Where(n => n >= 1 && n <= total)
            .Select(n => src.Pages[n - 1])
            .ToList();

        if (selected.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(request),
                $"None of the requested page numbers are in range 1-{total}.");

        return new DesignExportDto
        {
            Id             = Guid.NewGuid().ToString("N")[..12],
            Name           = request.NewName ?? $"{src.Name} (pages {string.Join(",", request.PageNumbers)})",
            Category       = src.Category,
            Description    = src.Description,
            PageSettings   = src.PageSettings,
            SharedElements = src.SharedElements,
            Pages          = selected,
        };
    }
}
