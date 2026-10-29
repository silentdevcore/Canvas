using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class CodeWorkspaceApplyTests
{
    private const string CodeDesign = """{"name":"Report","pages":[{"id":"page-code","elements":[]}]}""";
    private const string VisualDesign = """{"name":"Report","pages":[{"id":"page-visual","elements":[]}]}""";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Comparison_reads_the_current_template_without_rebasing_or_replacing_drafts()
    {
        await using var db = MemoryContext();
        var (template, workspace) = await Seed(db);
        var controller = Controller(db, template);
        var review = Read<CompareCodeWorkspaceResponse>(await controller.Compare(template.Id,
            new(workspace.Revision, "json", CodeDesign), default));

        Assert.Equal(8, review.TemplateRevision);
        Assert.Equal(4, review.BaseTemplateRevision);
        Assert.Equal("page-visual", review.TemplateDesign.GetProperty("pages")[0].GetProperty("id").GetString());
        Assert.Equal("page-code", review.Conversion.CanonicalDesign!.Pages[0].Id);
        Assert.Equal(4, workspace.BaseTemplateRevision);
        Assert.Equal("// original model", workspace.CSharpModelDraft);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task Explicitly_reviewed_apply_advances_the_base_and_keeps_other_language_drafts()
    {
        await using var db = MemoryContext();
        var (template, workspace) = await Seed(db);
        var controller = Controller(db, template);
        var review = Read<CompareCodeWorkspaceResponse>(await controller.Compare(template.Id, new(2, "json", CodeDesign), default));
        var applied = Read<ApplyCodeWorkspaceResponse>(await controller.Apply(template.Id,
            new(review.WorkspaceRevision, review.TemplateRevision, "json", CodeDesign, review.Conversion.CanonicalChecksum), default));

        Assert.Equal(9, applied.TemplateRevision);
        Assert.Equal(3, applied.WorkspaceRevision);
        Assert.Equal(9, workspace.BaseTemplateRevision);
        Assert.Equal("// original model", workspace.CSharpModelDraft);
        Assert.Equal("// original pdf", workspace.CSharpPdfDraft);
        Assert.Equal("// original base64", workspace.CSharpBase64Draft);
        Assert.Equal(CodeDesign, workspace.JsonDraft);
        Assert.Contains("page-code", template.DraftJson);
        Assert.Single(await db.AuditEvents.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Changes_after_comparison_are_rejected_without_replacing_drafts(bool changeTemplate)
    {
        await using var db = MemoryContext();
        var (template, workspace) = await Seed(db);
        var controller = Controller(db, template);
        var review = Read<CompareCodeWorkspaceResponse>(await controller.Compare(template.Id, new(2, "json", CodeDesign), default));
        if (changeTemplate) template.Revision++;
        else workspace.Revision++;
        await db.SaveChangesAsync();
        var result = await controller.Apply(template.Id,
            new(review.WorkspaceRevision, review.TemplateRevision, "json", CodeDesign, review.Conversion.CanonicalChecksum), default);
        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(VisualDesign, template.DraftJson);
        Assert.Equal(4, workspace.BaseTemplateRevision);
        Assert.Empty(await db.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task An_execution_result_that_differs_from_the_review_is_not_applied()
    {
        await using var db = MemoryContext();
        var (template, workspace) = await Seed(db);
        var result = await Controller(db, template).Apply(template.Id, new(2, 8, "json", CodeDesign, "changed-result"), default);
        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(VisualDesign, template.DraftJson);
        Assert.Equal(2, workspace.Revision);
    }

    [Fact]
    public async Task Stale_workspaces_missing_tenants_and_archived_or_foreign_templates_cannot_be_compared()
    {
        await using var db = MemoryContext();
        var (template, _) = await Seed(db);
        Assert.IsType<ConflictObjectResult>((await Controller(db, template).Compare(template.Id, new(1, "json", CodeDesign), default)).Result);
        var anonymous = Controller(db, template, new Tenant(null, null));
        Assert.IsType<UnauthorizedResult>((await anonymous.Compare(template.Id, new(2, "json", CodeDesign), default)).Result);
        var foreign = Controller(db, template, new Tenant(template.UpdatedByUserId, Guid.NewGuid()));
        Assert.IsType<NotFoundResult>((await foreign.Compare(template.Id, new(2, "json", CodeDesign), default)).Result);
        template.Status = DesignerTemplateStatus.Archived;
        await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>((await Controller(db, template).Compare(template.Id, new(2, "json", CodeDesign), default)).Result);
        Assert.Equal(PvadDesignerPermissions.CodeApply, Assert.Single(typeof(DesignerCodeWorkspacesController)
            .GetMethod("Compare")!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Policy);
    }

    [PostgreSqlFact]
    public async Task PostgreSql_concurrent_writes_during_conversion_roll_back_the_whole_apply()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using (var setup = new PvadDbContext(options)) await setup.Database.MigrateAsync();
        foreach (var changeTemplate in new[] { true, false })
        {
            await using var db = new PvadDbContext(options);
            var (template, workspace) = await Seed(db, relational: true);
            var conversion = new JsonConversion(async () =>
            {
                await using var competing = new PvadDbContext(options);
                if (changeTemplate)
                {
                    var other = await competing.DesignerTemplates.SingleAsync(value => value.Id == template.Id);
                    other.Revision++;
                    other.Name = "Concurrent update";
                }
                else
                {
                    var other = await competing.DesignerCodeWorkspaces.SingleAsync(value => value.Id == workspace.Id);
                    other.Revision++;
                    other.CSharpModelDraft = "// concurrent model";
                }
                await competing.SaveChangesAsync();
            });
            var result = await Controller(db, template, conversion: conversion).Apply(template.Id,
                new(2, 8, "json", CodeDesign, PvadCodeConversionService.Hash(CodeDesign)), default);
            Assert.IsType<ConflictObjectResult>(result.Result);
            await using var verify = new PvadDbContext(options);
            var persisted = await verify.DesignerTemplates.SingleAsync(value => value.Id == template.Id);
            var code = await verify.DesignerCodeWorkspaces.SingleAsync(value => value.Id == workspace.Id);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(VisualDesign), JsonNode.Parse(persisted.DraftJson)));
            Assert.Equal(changeTemplate ? 9 : 8, persisted.Revision);
            Assert.Equal(changeTemplate ? 2 : 3, code.Revision);
            Assert.Equal(4, code.BaseTemplateRevision);
            Assert.Equal(changeTemplate ? "// original model" : "// concurrent model", code.CSharpModelDraft);
            Assert.False(await verify.AuditEvents.AnyAsync(value => value.TargetId == workspace.Id.ToString()));
        }
    }

    private static PvadDbContext MemoryContext() => new(new DbContextOptionsBuilder<PvadDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static async Task<(DesignerTemplate, DesignerCodeWorkspace)> Seed(PvadDbContext db, bool relational = false)
    {
        var user = Guid.NewGuid(); var organization = Guid.NewGuid();
        if (relational)
        {
            db.Users.Add(new PvadIdentityUser { Id = user, DisplayName = "Synthetic tester" });
            db.Organizations.Add(new Organization { Id = organization, Name = "Synthetic", Slug = organization.ToString("N") });
        }
        var template = new DesignerTemplate
        {
            OrganizationId = organization, CreatedByUserId = user, UpdatedByUserId = user,
            Name = "Report", Revision = 8, DraftJson = VisualDesign, DraftChecksum = PvadCodeConversionService.Hash(VisualDesign),
            SchemaVersion = "1.0", DesignerVersion = "1.1.0",
        };
        var workspace = new DesignerCodeWorkspace
        {
            TemplateId = template.Id, OrganizationId = organization, UpdatedByUserId = user,
            Revision = 2, BaseTemplateRevision = 4, CanonicalDesignJson = CodeDesign, JsonDraft = CodeDesign,
            CSharpModelDraft = "// original model", CSharpPdfDraft = "// original pdf", CSharpBase64Draft = "// original base64",
        };
        db.AddRange(template, workspace); await db.SaveChangesAsync();
        return (template, workspace);
    }

    private static DesignerCodeWorkspacesController Controller(PvadDbContext db, DesignerTemplate template,
        IPvadTenantContext? tenant = null, IPvadCodeConversionService? conversion = null) => new(db,
            tenant ?? new Tenant(template.UpdatedByUserId, template.OrganizationId), conversion ?? new JsonConversion())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static T Read<T>(ActionResult<T> response) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(response.Result).Value);
    private sealed record Tenant(Guid? UserId, Guid? OrganizationId) : IPvadTenantContext;
    private sealed class JsonConversion(Func<Task>? beforeReturn = null) : IPvadCodeConversionService
    {
        public async Task<PvadCodeConversionResultDto> ConvertAsync(string from, string to, string source, CancellationToken cancellationToken)
        {
            if (beforeReturn is not null) await beforeReturn();
            return new PvadCodeConversionResultDto
            {
                SourceLanguage = from, TargetLanguage = to, Fidelity = "exact", DocumentFidelity = "exact", SourcePreservation = "preserved",
                CanonicalDesign = JsonSerializer.Deserialize<DesignExportDto>(source, JsonOptions), GeneratedSource = source,
                CanonicalChecksum = PvadCodeConversionService.Hash(source), SourceChecksum = "source", ResultChecksum = "result",
            };
        }
        public Task<PvadCodeWorkerResponse> ExecuteAsync(string language, string source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public PvadCodeConversionResultDto ValidateJson(string source) => throw new NotSupportedException();
    }
}
