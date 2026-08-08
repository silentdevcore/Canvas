using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Application.Identity;
using PXA.WebApi.Application.Legal;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class LegalDocumentGovernanceTests
{
    [Fact]
    public void Safe_renderer_encodes_raw_html_and_hashes_normalized_content()
    {
        const string markdown = "# Privacy\r\n\r\n<script>alert('x')</script>\r\n- Necessary sessions";

        var html = PxaLegalDocumentService.RenderSafeHtml(markdown);
        var windowsHash = PxaLegalDocumentService.ComputeHash(markdown);
        var unixHash = PxaLegalDocumentService.ComputeHash(markdown.Replace("\r\n", "\n"));

        Assert.Contains("<h1>Privacy</h1>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<li>Necessary sessions</li>", html, StringComparison.Ordinal);
        Assert.Equal(unixHash, windowsHash);
        Assert.Equal(64, windowsHash.Length);
    }

    [Fact]
    public void Legal_diff_aligns_replacements_and_reports_added_lines()
    {
        var result = PxaLegalDocumentDiff.Compare(
            "# Terms\nSame\nOld clause\nRemoved clause",
            "# Terms\nSame\nNew clause\nAdded clause\nAdditional clause");

        Assert.Equal(2, result.Unchanged);
        Assert.Equal(2, result.Modified);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Contains(result.Lines, value =>
            value.Kind == LegalDiffKind.Modified &&
            value.BaseText == "Old clause" &&
            value.TargetText == "New clause");
        Assert.Contains(result.Lines, value =>
            value.Kind == LegalDiffKind.Added &&
            value.BaseLineNumber is null &&
            value.TargetText == "Additional clause");
    }

    [Fact]
    public async Task Public_api_returns_only_the_effective_published_version()
    {
        await using var context = CreateContext();
        var document = NewDocument(Guid.NewGuid());
        var published = NewVersion(document.Id, "2026-07", LegalDocumentStatus.Published, DateTimeOffset.UtcNow.AddDays(-1));
        var draft = NewVersion(document.Id, "2026-08", LegalDocumentStatus.Draft, null);
        context.AddRange(document, published, draft);
        await context.SaveChangesAsync();
        var service = new PxaLegalDocumentService(context);
        var controller = new LegalDocumentsController(context, service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var response = await controller.GetCurrent(
            "terms", "de", LegalDocumentAudience.All, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<PublicLegalDocumentResponse>(ok.Value);
        Assert.Equal("2026-07", body.Version);
        Assert.Equal(published.ContentHash, body.ContentHash);
        Assert.Equal($"\"{published.ContentHash}\"", controller.Response.Headers.ETag);
    }

    [Fact]
    public async Task Snapshot_api_contains_only_current_effective_documents_in_stable_key_order()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var termsDocument = NewDocument(userId);
        var privacyDocument = new LegalDocument
        {
            Type = LegalDocumentType.PrivacyNotice,
            Key = "privacy",
            DisplayName = "Privacy",
            CreatedByUserId = userId,
        };
        var terms = NewVersion(
            termsDocument.Id,
            "terms-current",
            LegalDocumentStatus.Published,
            DateTimeOffset.UtcNow.AddDays(-2));
        var privacy = NewVersion(
            privacyDocument.Id,
            "privacy-current",
            LegalDocumentStatus.Scheduled,
            DateTimeOffset.UtcNow.AddDays(-1));
        var future = NewVersion(
            privacyDocument.Id,
            "privacy-future",
            LegalDocumentStatus.Scheduled,
            DateTimeOffset.UtcNow.AddDays(1));
        context.AddRange(termsDocument, privacyDocument, terms, privacy, future);
        await context.SaveChangesAsync();
        var controller = new LegalDocumentsController(
            context,
            new PxaLegalDocumentService(context))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var response = await controller.GetSnapshot(
            "de", LegalDocumentAudience.All, CancellationToken.None);

        var body = Assert.IsType<LegalDocumentSnapshotResponse>(
            Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(1, body.SchemaVersion);
        Assert.Equal("de", body.Locale);
        Assert.Equal("All", body.Audience);
        Assert.Equal(["privacy", "terms"], body.Documents.Select(value => value.Key));
        Assert.DoesNotContain(body.Documents, value => value.Version == "privacy-future");
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Registration_policy_uses_configured_fallback_outside_strict_mode()
    {
        await using var context = CreateContext();
        var service = new RegistrationLegalPolicyService(
            new PxaLegalDocumentService(context),
            Options.Create(new PxaRegistrationOptions
            {
                TermsVersion = "terms-fallback",
                PrivacyVersion = "privacy-fallback",
                RequireDatabaseLegalDocuments = false,
            }));

        var policy = await service.ResolveAsync("en", CancellationToken.None);

        Assert.True(policy.Available);
        Assert.False(policy.DatabaseBacked);
        Assert.Equal("terms-fallback", policy.Terms!.Version);
        Assert.Equal("privacy-fallback", policy.Privacy!.Version);
        Assert.Null(policy.Terms.Id);
        Assert.Null(policy.Privacy.Id);
    }

    [Fact]
    public async Task Registration_policy_fails_closed_in_strict_mode_without_published_documents()
    {
        await using var context = CreateContext();
        var service = new RegistrationLegalPolicyService(
            new PxaLegalDocumentService(context),
            Options.Create(new PxaRegistrationOptions
            {
                TermsVersion = "terms-fallback",
                PrivacyVersion = "privacy-fallback",
                RequireDatabaseLegalDocuments = true,
            }));

        var policy = await service.ResolveAsync("de", CancellationToken.None);

        Assert.False(policy.Available);
        Assert.False(policy.DatabaseBacked);
        Assert.Null(policy.Terms);
        Assert.Null(policy.Privacy);
    }

    [Fact]
    public async Task Consumer_checkout_fails_closed_without_every_required_effective_document()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var terms = NewDocument(actorId);
        var termsVersion = NewVersion(
            terms.Id,
            "consumer-terms-1",
            LegalDocumentStatus.Published,
            DateTimeOffset.UtcNow.AddMinutes(-1));
        termsVersion.Audience = LegalDocumentAudience.Consumer;
        context.AddRange(terms, termsVersion);
        await context.SaveChangesAsync();
        var gate = new PxaConsumerCheckoutLegalGate(
            new PxaLegalDocumentService(context),
            Options.Create(new PxaConsumerCheckoutOptions { Enabled = true }));

        var readiness = await gate.EvaluateAsync(
            "de", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(readiness.Available);
        Assert.True(readiness.CommerciallyEnabled);
        Assert.False(readiness.LegalDocumentsReady);
        Assert.Equal("required-legal-documents-unavailable", readiness.Reason);
        Assert.Collection(
            readiness.Documents,
            document => Assert.True(document.Available),
            document => Assert.False(document.Available),
            document => Assert.False(document.Available));
    }

    [Fact]
    public async Task Consumer_checkout_requires_both_commercial_enablement_and_legal_readiness()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        foreach (var (type, key) in new[]
                 {
                     (LegalDocumentType.TermsAndConditions, "terms"),
                     (LegalDocumentType.PrivacyNotice, "privacy"),
                     (LegalDocumentType.ConsumerWithdrawal, "withdrawal"),
                 })
        {
            var document = new LegalDocument
            {
                Type = type,
                Key = key,
                DisplayName = key,
                CreatedByUserId = actorId,
            };
            var version = NewVersion(
                document.Id,
                $"{key}-1",
                LegalDocumentStatus.Published,
                DateTimeOffset.UtcNow.AddMinutes(-1));
            version.Audience = LegalDocumentAudience.Consumer;
            context.AddRange(document, version);
        }
        await context.SaveChangesAsync();
        var legalDocuments = new PxaLegalDocumentService(context);

        var disabled = await new PxaConsumerCheckoutLegalGate(
                legalDocuments,
                Options.Create(new PxaConsumerCheckoutOptions()))
            .EvaluateAsync("de", DateTimeOffset.UtcNow, CancellationToken.None);
        var enabled = await new PxaConsumerCheckoutLegalGate(
                legalDocuments,
                Options.Create(new PxaConsumerCheckoutOptions { Enabled = true }))
            .EvaluateAsync("de", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(disabled.Available);
        Assert.True(disabled.LegalDocumentsReady);
        Assert.Equal("consumer-checkout-disabled", disabled.Reason);
        Assert.True(enabled.Available);
        Assert.True(enabled.LegalDocumentsReady);
        Assert.Null(enabled.Reason);
        Assert.All(enabled.Documents, document =>
        {
            Assert.NotNull(document.VersionId);
            Assert.NotNull(document.ContentHash);
        });
    }

    [Fact]
    public void Legal_acceptance_evidence_does_not_store_network_addresses()
    {
        var propertyNames = typeof(LegalAcceptanceEvent)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("Ip", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Four_eyes_rule_blocks_author_and_allows_independent_reviewer()
    {
        await using var context = CreateContext();
        var authorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var author = CreateAdminController(context, authorId);
        var createdDocument = await author.CreateDocument(
            new CreateLegalDocumentRequest(
                nameof(LegalDocumentType.TermsAndConditions), "terms", "Terms"),
            CancellationToken.None);
        var document = Assert.IsType<AdminLegalDocumentResponse>(
            Assert.IsType<CreatedResult>(createdDocument.Result).Value);
        var createdVersion = await author.CreateVersion(
            document.Id,
            new CreateLegalVersionRequest(
                "2026-07", "de", "All", "# Terms", "Initial draft", true, true),
            CancellationToken.None);
        var version = Assert.IsType<AdminLegalVersionResponse>(
            Assert.IsType<CreatedResult>(createdVersion.Result).Value);
        await author.Submit(version.Id, CancellationToken.None);

        var selfReview = await author.Review(
            version.Id, new ReviewLegalVersionRequest(true, null), CancellationToken.None);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(selfReview.Result).StatusCode);

        var reviewer = CreateAdminController(context, reviewerId);
        var approval = await reviewer.Review(
            version.Id, new ReviewLegalVersionRequest(true, "Reviewed"), CancellationToken.None);
        Assert.Equal(
            LegalDocumentStatus.Approved.ToString(),
            Assert.IsType<AdminLegalVersionResponse>(
                Assert.IsType<OkObjectResult>(approval.Result).Value).Status);

        var publication = await reviewer.Publish(
            version.Id, new PublishLegalVersionRequest(null), CancellationToken.None);
        Assert.Equal(
            LegalDocumentStatus.Published.ToString(),
            Assert.IsType<AdminLegalVersionResponse>(
                Assert.IsType<OkObjectResult>(publication.Result).Value).Status);
        Assert.Single(await context.LegalPublicationApprovals.ToListAsync());
        Assert.Contains(await context.AuditEvents.Select(value => value.Action).ToListAsync(),
            value => value == "legal.version.published");
    }

    [Fact]
    public async Task Successor_review_and_publication_require_its_recorded_predecessor_comparison()
    {
        await using var context = CreateContext();
        var authorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var document = NewDocument(authorId);
        var predecessor = NewVersion(
            document.Id,
            "2026-07",
            LegalDocumentStatus.Published,
            DateTimeOffset.UtcNow.AddDays(-1));
        var successor = NewVersion(
            document.Id,
            "2026-08",
            LegalDocumentStatus.InReview,
            null);
        successor.SourceMarkdown = "# Terms\nUpdated";
        successor.RenderedHtml = "<h1>Terms</h1><p>Updated</p>";
        successor.ContentHash = PxaLegalDocumentService.ComputeHash(successor.SourceMarkdown);
        successor.CreatedByUserId = authorId;
        successor.PreviousVersionId = predecessor.Id;
        context.AddRange(document, predecessor, successor);
        await context.SaveChangesAsync();
        var reviewer = CreateAdminController(context, reviewerId);

        var missingReviewComparison = await reviewer.Review(
            successor.Id,
            new ReviewLegalVersionRequest(true, null),
            CancellationToken.None);
        Assert.Equal(
            StatusCodes.Status409Conflict,
            Assert.IsType<ObjectResult>(missingReviewComparison.Result).StatusCode);

        var comparison = await reviewer.CompareVersions(
            predecessor.Id,
            successor.Id,
            CancellationToken.None);
        var comparisonBody = Assert.IsType<AdminLegalVersionComparisonResponse>(
            Assert.IsType<OkObjectResult>(comparison.Result).Value);
        Assert.Equal(predecessor.Id, comparisonBody.BaseVersion.Id);
        Assert.Equal(successor.Id, comparisonBody.TargetVersion.Id);
        Assert.True(comparisonBody.Summary.Modified + comparisonBody.Summary.Added > 0);

        var approval = await reviewer.Review(
            successor.Id,
            new ReviewLegalVersionRequest(true, "Compared", predecessor.Id),
            CancellationToken.None);
        Assert.Equal(
            LegalDocumentStatus.Approved.ToString(),
            Assert.IsType<AdminLegalVersionResponse>(
                Assert.IsType<OkObjectResult>(approval.Result).Value).Status);

        var missingPublishComparison = await reviewer.Publish(
            successor.Id,
            new PublishLegalVersionRequest(null),
            CancellationToken.None);
        Assert.Equal(
            StatusCodes.Status409Conflict,
            Assert.IsType<ObjectResult>(missingPublishComparison.Result).StatusCode);

        var publication = await reviewer.Publish(
            successor.Id,
            new PublishLegalVersionRequest(null, predecessor.Id),
            CancellationToken.None);
        Assert.Equal(
            LegalDocumentStatus.Published.ToString(),
            Assert.IsType<AdminLegalVersionResponse>(
                Assert.IsType<OkObjectResult>(publication.Result).Value).Status);
    }

    [Fact]
    public async Task Comparison_rejects_versions_from_different_documents_and_requires_read_policy()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var terms = NewDocument(userId);
        var privacy = new LegalDocument
        {
            Type = LegalDocumentType.PrivacyNotice,
            Key = "privacy",
            DisplayName = "Privacy",
            CreatedByUserId = userId,
        };
        var termsVersion = NewVersion(
            terms.Id, "terms-v1", LegalDocumentStatus.Draft, null);
        var privacyVersion = NewVersion(
            privacy.Id, "privacy-v1", LegalDocumentStatus.Draft, null);
        context.AddRange(terms, privacy, termsVersion, privacyVersion);
        await context.SaveChangesAsync();
        var controller = CreateAdminController(context, userId);

        var response = await controller.CompareVersions(
            termsVersion.Id,
            privacyVersion.Id,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ObjectResult>(response.Result).StatusCode);
        var authorization = typeof(AdminLegalDocumentsController)
            .GetMethod(nameof(AdminLegalDocumentsController.CompareVersions))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal(PxaPermissions.LegalRead, authorization.Policy);
    }

    [Fact]
    public async Task Published_legal_content_is_immutable_but_can_be_retired()
    {
        await using var context = CreateContext();
        var document = NewDocument(Guid.NewGuid());
        var version = NewVersion(document.Id, "2026-07", LegalDocumentStatus.Published, DateTimeOffset.UtcNow);
        context.AddRange(document, version);
        await context.SaveChangesAsync();

        version.SourceMarkdown = "# Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        context.Entry(version).CurrentValues.SetValues(context.Entry(version).OriginalValues);
        context.Entry(version).State = EntityState.Unchanged;
        version.Status = LegalDocumentStatus.Retired;
        version.RetiredAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Acceptance_summary_counts_exact_version_and_export_omits_identity_fields()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var organization = new Organization
        {
            Name = "Example Company",
            Slug = "example-company",
        };
        var acceptedUser = NewUser("accepted@example.test");
        var pendingUser = NewUser("pending@example.test");
        var document = NewDocument(actorId);
        var version = NewVersion(
            document.Id,
            "terms-2026-08",
            LegalDocumentStatus.Published,
            DateTimeOffset.UtcNow.AddMinutes(-1));
        version.RequiresAcceptance = true;
        context.AddRange(
            organization,
            acceptedUser,
            pendingUser,
            document,
            version,
            new OrganizationMembership
            {
                OrganizationId = organization.Id,
                UserId = acceptedUser.Id,
            },
            new OrganizationMembership
            {
                OrganizationId = organization.Id,
                UserId = pendingUser.Id,
            },
            new OrganizationSubscription
            {
                OrganizationId = organization.Id,
                Edition = SubscriptionEdition.Premium,
                AccountType = SubscriptionAccountType.Company,
                Status = SubscriptionStatus.Active,
                BillingPeriod = SubscriptionBillingPeriod.Monthly,
            },
            new LegalAcceptanceEvent
            {
                UserId = acceptedUser.Id,
                OrganizationId = organization.Id,
                LegalDocumentVersionId = version.Id,
                DocumentType = "TermsAndConditions",
                Decision = "accepted",
                ContentHash = version.ContentHash,
                Locale = "de",
                Source = "account-legal-review",
            });
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.OrganizationMemberships.CountAsync());
        Assert.Equal(2, await (
            from membership in context.OrganizationMemberships
            join user in context.Users on membership.UserId equals user.Id
            join activeOrganization in context.Organizations
                on membership.OrganizationId equals activeOrganization.Id
            where membership.Status == OrganizationMembershipStatus.Active &&
                  user.IsActive &&
                  activeOrganization.Status == OrganizationStatus.Active
            select membership).CountAsync());
        var controller = CreateAdminController(context, actorId);

        var response = await controller.GetAcceptanceSummary(
            version.Id, null, null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var summary = Assert.IsType<AdminLegalAcceptanceSummaryResponse>(ok.Value);
        Assert.Equal(2, summary.AffectedAccounts);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(1, summary.Pending);
        Assert.Equal(50m, summary.CompletionPercentage);
        Assert.Contains(summary.ByAccountType, value =>
            value.Name == "Company" && value.AffectedAccounts == 2);

        var export = await controller.ExportAcceptanceEvidence(
            version.Id,
            new AdminLegalAcceptanceExportRequest("csv"),
            CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(export);
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains(version.ContentHash, csv, StringComparison.Ordinal);
        Assert.DoesNotContain("accepted@example.test", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("pending@example.test", csv, StringComparison.Ordinal);
        var audit = await context.AuditEvents.SingleAsync(value =>
            value.Action == "legal.acceptance.exported");
        Assert.Contains("\"Rows\":1", audit.DetailsJson, StringComparison.Ordinal);
        Assert.Contains("\"Format\":\"csv\"", audit.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("accepted@example.test", audit.DetailsJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("terms", LegalDocumentType.TermsAndConditions)]
    [InlineData("cookie-storage", LegalDocumentType.CookieAndStoragePolicy)]
    [InlineData("dpa", LegalDocumentType.DataProcessingAgreement)]
    public void Public_type_aliases_are_stable(string value, LegalDocumentType expected)
    {
        Assert.True(LegalDocumentsController.TryParseType(value, out var actual));
        Assert.Equal(expected, actual);
    }

    private static AdminLegalDocumentsController CreateAdminController(PxaDbContext context, Guid userId) =>
        new(context, new StubTenantContext(userId))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static PxaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PxaDbContext(options);
    }

    private static LegalDocument NewDocument(Guid userId) => new()
    {
        Type = LegalDocumentType.TermsAndConditions,
        Key = "terms",
        DisplayName = "Terms",
        CreatedByUserId = userId,
    };

    private static LegalDocumentVersion NewVersion(
        Guid documentId,
        string version,
        LegalDocumentStatus status,
        DateTimeOffset? effectiveAt) => new()
    {
        LegalDocumentId = documentId,
        Version = version,
        Locale = "de",
        Audience = LegalDocumentAudience.All,
        Status = status,
        SourceMarkdown = $"# Terms {version}",
        RenderedHtml = $"<h1>Terms {version}</h1>",
        ContentHash = PxaLegalDocumentService.ComputeHash($"# Terms {version}"),
        CreatedByUserId = Guid.NewGuid(),
        EffectiveAt = effectiveAt,
        PublishedAt = effectiveAt,
    };

    private static PxaIdentityUser NewUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        EmailConfirmed = true,
        DisplayName = email,
        Locale = "de",
    };

    private sealed class StubTenantContext(Guid userId) : IPxaTenantContext
    {
        public Guid? UserId { get; } = userId;
        public Guid? OrganizationId => null;
    }
}
