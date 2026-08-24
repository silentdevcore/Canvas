using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
[Route("api/pxa/v1/designer/preferences/element-favorites")]
public sealed class DesignerElementFavoritesController(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PxaDesignerPermissions.PreferencesManage)]
    public async Task<ActionResult<DesignerElementFavoritesResponse>> List(
        CancellationToken cancellationToken)
    {
        if (tenantContext.UserId is not { } userId || userId == Guid.Empty)
            return Unauthorized();

        var favorites = await dbContext.DesignerElementFavorites
            .Where(value => value.UserId == userId)
            .OrderBy(value => value.CreatedAt)
            .Select(value => value.ElementType)
            .ToListAsync(cancellationToken);

        return Ok(new DesignerElementFavoritesResponse(OrderByCatalog(favorites)));
    }

    [HttpPut]
    [Authorize(Policy = PxaDesignerPermissions.PreferencesManage)]
    public async Task<ActionResult<DesignerElementFavoritesResponse>> Replace(
        ReplaceDesignerElementFavoritesRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.UserId is not { } userId || userId == Guid.Empty)
            return Unauthorized();

        var requested = request.ElementTypes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var unknown = requested.Where(value => !PxaDesignerElementCatalog.ElementTypeSet.Contains(value)).ToArray();
        if (unknown.Length > 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unknown Designer element type",
                Detail = $"Unsupported element types: {string.Join(", ", unknown)}",
            });
        }

        var existing = await dbContext.DesignerElementFavorites
            .Where(value => value.UserId == userId)
            .ToListAsync(cancellationToken);
        var requestedSet = requested.ToHashSet(StringComparer.Ordinal);
        dbContext.DesignerElementFavorites.RemoveRange(
            existing.Where(value => !requestedSet.Contains(value.ElementType)));

        var existingTypes = existing.Select(value => value.ElementType).ToHashSet(StringComparer.Ordinal);
        foreach (var elementType in requested.Where(value => !existingTypes.Contains(value)))
        {
            dbContext.DesignerElementFavorites.Add(new DesignerElementFavorite
            {
                UserId = userId,
                ElementType = elementType,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new DesignerElementFavoritesResponse(OrderByCatalog(requested)));
    }

    private static IReadOnlyList<string> OrderByCatalog(IEnumerable<string> values)
    {
        var selected = values.ToHashSet(StringComparer.Ordinal);
        return PxaDesignerElementCatalog.ElementTypes.Where(selected.Contains).ToArray();
    }
}

public sealed record ReplaceDesignerElementFavoritesRequest(IReadOnlyList<string> ElementTypes);
public sealed record DesignerElementFavoritesResponse(IReadOnlyList<string> ElementTypes);
