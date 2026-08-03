using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Roles = PxaRoles.SystemAdministrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pxa/v1/admin/operator/documentation")]
public sealed class OperatorDocumentationController(
    IWebHostEnvironment environment,
    UserManager<PxaIdentityUser> userManager,
    PxaSystemOperatorAccess operatorAccess,
    PxaDbContext dbContext) : ControllerBase
{
    private static readonly OperatorDocumentDescriptor[] Documents =
    [
        new(
            "admin-operations",
            "PXA Admin Operations",
            "Hosting, observability, alerting, security, and platform recovery boundaries.",
            "PXA.Admin-Operations.md"),
        new(
            "legal-backup-restore-recovery",
            "Legal Backup, Restore, and Disaster Recovery",
            "Full-database Legal continuity, verification, rollback, and isolated recovery drills.",
            "PXA.Legal-Backup-Restore-And-Recovery.md"),
    ];

    private readonly string documentationRoot = ResolveDocumentationRoot(environment);

    [HttpGet]
    public async Task<ActionResult<OperatorDocumentationCatalogResponse>> GetCatalog()
    {
        var user = await GetAuthorizedOperatorAsync();
        if (user is null)
            return Forbid();

        var available = Documents
            .Where(document => System.IO.File.Exists(Path.Combine(documentationRoot, document.FileName)))
            .Select(document => new OperatorDocumentationCatalogItem(
                document.Slug,
                document.Title,
                document.Summary,
                $"/api/pxa/v1/admin/operator/documentation/{document.Slug}"))
            .ToArray();
        return Ok(new OperatorDocumentationCatalogResponse(available));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<OperatorDocumentationResponse>> GetDocument(
        string slug,
        CancellationToken cancellationToken)
    {
        var user = await GetAuthorizedOperatorAsync();
        if (user is null)
            return Forbid();

        var descriptor = Documents.SingleOrDefault(document =>
            string.Equals(document.Slug, slug, StringComparison.Ordinal));
        if (descriptor is null)
            return NotFound();

        var path = Path.Combine(documentationRoot, descriptor.FileName);
        if (!System.IO.File.Exists(path))
            return DocumentationUnavailable();

        var markdown = await System.IO.File.ReadAllTextAsync(path, cancellationToken);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = user.Id,
            Action = "operator.documentation.read",
            TargetType = "OperatorRunbook",
            TargetId = descriptor.Slug,
            Outcome = "succeeded",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new OperatorDocumentationResponse(
            descriptor.Slug,
            descriptor.Title,
            descriptor.Summary,
            markdown));
    }

    private async Task<PxaIdentityUser?> GetAuthorizedOperatorAsync()
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is null)
            return null;
        var user = await userManager.GetUserAsync(User);
        return user is not null && operatorAccess.IsAuthorized(user) ? user : null;
    }

    private static string ResolveDocumentationRoot(IWebHostEnvironment environment)
    {
        var packaged = Path.Combine(environment.ContentRootPath, "OperatorDocumentation");
        if (Directory.Exists(packaged))
            return packaged;
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "operator-docs"));
    }

    private ObjectResult DocumentationUnavailable() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Operator documentation unavailable",
        detail: "The protected operator runbooks are not installed in this deployment.");

    private sealed record OperatorDocumentDescriptor(
        string Slug,
        string Title,
        string Summary,
        string FileName);
}

public sealed record OperatorDocumentationCatalogResponse(
    IReadOnlyList<OperatorDocumentationCatalogItem> Documents);

public sealed record OperatorDocumentationCatalogItem(
    string Slug,
    string Title,
    string Summary,
    string Href);

public sealed record OperatorDocumentationResponse(
    string Slug,
    string Title,
    string Summary,
    string Markdown);
