using PVAD.Core.Contracts;

namespace PVAD.Application.UseCases;

public sealed class CloneDesignRequest
{
    public required DesignExportDto Design { get; set; }
    public string? NewName { get; set; }
}

/// <summary>
/// Deep-clones a <see cref="DesignExportDto"/> into a new independent instance
/// with a fresh ID and optional new name. All pages, elements, and settings are
/// copied so mutations to the clone do not affect the original.
/// </summary>
public sealed class CloneTemplateUseCase
{
    public DesignExportDto Execute(CloneDesignRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var src  = request.Design;
        var newId = Guid.NewGuid().ToString("N")[..12];

        var clone = new DesignExportDto
        {
            Id          = newId,
            Name        = request.NewName ?? $"{src.Name} (Copy)",
            Category    = src.Category,
            Description = src.Description,
            PageSettings = ClonePageSettings(src.PageSettings),
            SharedElements = src.SharedElements.Select(CloneElement).ToList(),
            Pages = src.Pages.Select(p => new PageDto
            {
                Id       = Guid.NewGuid().ToString("N")[..8],
                Layout   = ClonePageLayout(p.Layout),
                Elements = p.Elements.Select(CloneElement).ToList(),
            }).ToList(),
        };

        return clone;
    }

    private static PageSettingsDto? ClonePageSettings(PageSettingsDto? s)
    {
        if (s is null) return null;
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        return System.Text.Json.JsonSerializer.Deserialize<PageSettingsDto>(json);
    }

    private static PageLayoutDto? ClonePageLayout(PageLayoutDto? layout)
    {
        if (layout is null) return null;
        var json = System.Text.Json.JsonSerializer.Serialize(layout);
        return System.Text.Json.JsonSerializer.Deserialize<PageLayoutDto>(json);
    }

    private static ElementDto CloneElement(ElementDto el)
    {
        // Shallow-copy via MemberwiseClone isn't accessible on sealed DTO.
        // Instead reproduce by serialising to JSON and back for full fidelity.
        var json  = System.Text.Json.JsonSerializer.Serialize(el);
        var clone = System.Text.Json.JsonSerializer.Deserialize<ElementDto>(json)!;
        clone.Id  = Guid.NewGuid().ToString("N")[..8];
        return clone;
    }
}
