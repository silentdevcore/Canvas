using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkiaSharp;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Storage;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
[Route("api/pxa/v1/designer/assets")]
public sealed class DesignerAssetsController(
    IPxaTenantContext tenantContext,
    PxaStoredObjectService storedObjects,
    IOptions<PxaStorageOptions> options) : ControllerBase
{
    public const string AssetPurpose = "designer-image";

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<DesignerAssetResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DesignerAssetResponse>> Upload(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();
        if (file is null || file.Length == 0)
            return ProblemResult(400, "PXA_DESIGNER_ASSET_REQUIRED", "A PNG or JPEG image is required.");
        if (file.Length > options.Value.MaximumDesignerAssetBytes)
            return ProblemResult(413, "PXA_DESIGNER_ASSET_TOO_LARGE", $"Images may not exceed {options.Value.MaximumDesignerAssetBytes} bytes.");

        await using var source = file.OpenReadStream();
        using var buffered = new MemoryStream((int)file.Length);
        await source.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;
        if (!TryInspectImage(buffered.ToArray(), out var contentType, out var width, out var height))
            return ProblemResult(415, "PXA_DESIGNER_ASSET_TYPE_UNSUPPORTED", "Only valid PNG and JPEG images are supported.");

        buffered.Position = 0;
        var stored = await storedObjects.StoreAsync(
            organizationId,
            userId,
            AssetPurpose,
            contentType,
            file.FileName,
            buffered,
            cancellationToken);
        var response = ToResponse(stored.Id, stored.FileName, stored.ContentType, stored.Length,
            stored.Checksum, width, height, stored.CreatedAt);
        return Created(response.ContentUrl, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DesignerAssetResponse>> GetMetadata(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var stored = await storedObjects.GetMetadataAsync(id, organizationId, AssetPurpose, cancellationToken);
        return stored is null
            ? NotFound()
            : Ok(ToResponse(stored.Id, stored.FileName, stored.ContentType, stored.Length,
                stored.Checksum, null, null, stored.CreatedAt));
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var result = await storedObjects.OpenAsync(id, organizationId, AssetPurpose, cancellationToken);
        if (result is null)
            return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        Response.Headers.ETag = $"\"{result.Value.Metadata.Checksum}\"";
        return File(result.Value.Content, result.Value.Metadata.ContentType, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var stored = await storedObjects.GetMetadataAsync(id, organizationId, AssetPurpose, cancellationToken);
        if (stored is null)
            return NotFound();
        await storedObjects.DeleteAsync(id, organizationId, cancellationToken);
        return NoContent();
    }

    private bool TryGetContext(out Guid organizationId, out Guid userId)
    {
        organizationId = tenantContext.OrganizationId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return organizationId != Guid.Empty && userId != Guid.Empty;
    }

    private static bool TryInspectImage(byte[] content, out string contentType, out int width, out int height)
    {
        contentType = string.Empty;
        width = 0;
        height = 0;
        using var encoded = new SKMemoryStream(content);
        using var codec = SKCodec.Create(encoded);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
            return false;
        contentType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            _ => string.Empty,
        };
        if (contentType.Length == 0)
            return false;
        width = codec.Info.Width;
        height = codec.Info.Height;
        return (long)width * height <= 100_000_000;
    }

    private static DesignerAssetResponse ToResponse(
        Guid id, string? fileName, string contentType, long length, string checksum,
        int? width, int? height, DateTimeOffset createdAt) =>
        new(id, fileName, contentType, length, checksum, width, height,
            $"/api/pxa/v1/designer/assets/{id}/content", createdAt);

    private ObjectResult ProblemResult(int status, string code, string detail) =>
        StatusCode(status, new ProblemDetails
        {
            Status = status,
            Title = "Designer asset rejected",
            Detail = detail,
            Extensions = { ["code"] = code },
        });
}

public sealed record DesignerAssetResponse(
    Guid Id,
    string? FileName,
    string ContentType,
    long Length,
    string Checksum,
    int? Width,
    int? Height,
    string ContentUrl,
    DateTimeOffset CreatedAt);
