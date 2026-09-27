using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Generator;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

/// <summary>
/// EINV-11: validation verdicts are retained per tenant, never modified, bounded by retention,
/// exportable, audited, and revalidatable against a newer artifact set.
/// </summary>
public sealed class PdfEInvoiceValidationEvidenceTests
{
    private static readonly byte[] InvoiceXml = Encoding.UTF8.GetBytes("""
        <rsm:CrossIndustryInvoice
          xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
          xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
          <rsm:ExchangedDocumentContext>
            <ram:GuidelineSpecifiedDocumentContextParameter>
              <ram:ID>urn:factur-x.eu:1p0:basic</ram:ID>
            </ram:GuidelineSpecifiedDocumentContextParameter>
          </rsm:ExchangedDocumentContext>
          <rsm:ExchangedDocument><ram:ID>EVID-1</ram:ID></rsm:ExchangedDocument>
        </rsm:CrossIndustryInvoice>
        """);

    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ActorA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorB = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task RecordAsync_StoresTheCompleteEvidenceAndAnAuditEvent()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext, TenantA, ActorA);

        var stored = await service.RecordAsync(Entry());

        Assert.Equal("validate", stored.Operation);
        Assert.Equal(Sha256(InvoiceXml), stored.DocumentSha256);
        Assert.Equal("FacturX", stored.Standard);
        Assert.Equal(EInvoice.CurrentFacturXVersion, stored.StandardVersion);
        Assert.Equal("BASIC", stored.Profile);
        Assert.Equal("CII", stored.Syntax);
        Assert.True(stored.IsStructurallyValid);
        Assert.False(stored.IsConformanceValidated);
        Assert.Equal("notEvaluated", stored.ConformanceStatus);
        Assert.Equal(ActorA, stored.ActorUserId);
        Assert.True(stored.RetainUntil > stored.CreatedAt);
        Assert.NotEmpty(stored.Diagnostics);

        var audit = Assert.Single(await dbContext.AuditEvents.ToArrayAsync());
        Assert.Equal("pdf.e-invoice.validate", audit.Action);
        Assert.Equal("e-invoice-validation-record", audit.TargetType);
        Assert.Equal(stored.Id.ToString(), audit.TargetId);
        Assert.Equal(TenantA, audit.OrganizationId);
        Assert.Equal(ActorA, audit.ActorUserId);
        // The audit trail must carry checksums and codes, never invoice content.
        Assert.Contains(stored.DocumentSha256, audit.DetailsJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("EVID-1", audit.DetailsJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("CrossIndustryInvoice", audit.DetailsJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordAsync_KeepsTheArtifactSetIdentityOfAConformanceVerdict()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext, TenantA, ActorA);

        var stored = await service.RecordAsync(Entry(report: Report(
            conformanceStatus: "validated",
            isConformanceValidated: true,
            artifactSetId: "ferd-zugferd-2.5.2-en16931",
            artifactSetSha256: new string('c', 64))));

        Assert.True(stored.IsConformanceValidated);
        Assert.Equal("validated", stored.ConformanceStatus);
        Assert.Equal("ferd-zugferd-2.5.2-en16931", stored.ValidatorArtifactSetId);
        Assert.Equal(new string('c', 64), stored.ValidatorArtifactSetSha256);
    }

    [Fact]
    public async Task GetHistoryAsync_NeverReturnsAnotherTenantsEvidence()
    {
        await using var dbContext = CreateContext();
        var ownRecord = await CreateService(dbContext, TenantA, ActorA).RecordAsync(Entry());
        await CreateService(dbContext, TenantB, ActorB).RecordAsync(Entry());

        var ownHistory = await CreateService(dbContext, TenantA, ActorA).GetHistoryAsync(null, 50);
        var otherHistory = await CreateService(dbContext, TenantB, ActorB).GetHistoryAsync(null, 50);

        Assert.Equal(ownRecord.Id, Assert.Single(ownHistory).Id);
        Assert.NotEqual(ownRecord.Id, Assert.Single(otherHistory).Id);
        Assert.Equal(2, await dbContext.EInvoiceValidationRecords.CountAsync());
    }

    [Fact]
    public async Task GetAsync_NeverResolvesAnotherTenantsRecordById()
    {
        await using var dbContext = CreateContext();
        var foreignRecord = await CreateService(dbContext, TenantB, ActorB).RecordAsync(Entry());

        var resolved = await CreateService(dbContext, TenantA, ActorA).GetAsync(foreignRecord.Id);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task RecordAsync_RefusesToChainOntoAnotherTenantsRecord()
    {
        await using var dbContext = CreateContext();
        var foreignRecord = await CreateService(dbContext, TenantB, ActorB).RecordAsync(Entry());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(dbContext, TenantA, ActorA)
                .RecordAsync(Entry() with { RevalidatesRecordId = foreignRecord.Id }));
    }

    [Fact]
    public async Task RecordAsync_RequiresAnAuthenticatedTenantContext()
    {
        await using var dbContext = CreateContext();
        var service = new PxaEInvoiceValidationEvidenceService(
            dbContext,
            new TestTenantContext(null, null),
            Options.Create(new PxaEInvoiceEvidenceOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(Entry()));
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    public async Task RecordAsync_RejectsAChecksumThatIsNotLowercaseSha256(string checksum)
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext, TenantA, ActorA);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordAsync(Entry() with { DocumentSha256 = checksum }));
    }

    [Fact]
    public async Task GetHistoryAsync_HidesRecordsWhoseRetentionWindowHasClosed()
    {
        await using var dbContext = CreateContext();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateService(dbContext, TenantA, ActorA, new FixedClock(start), retentionDays: 30)
            .RecordAsync(Entry());

        var afterExpiry = CreateService(
            dbContext, TenantA, ActorA, new FixedClock(start.AddDays(31)), retentionDays: 30);

        Assert.Empty(await afterExpiry.GetHistoryAsync(null, 50));
        // The row is still there until retention removes it; it is hidden, not silently altered.
        Assert.Equal(1, await dbContext.EInvoiceValidationRecords.CountAsync());
    }

    [Fact]
    public async Task PurgeExpiredAsync_RemovesOnlyExpiredRecords()
    {
        await using var dbContext = CreateContext();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateService(dbContext, TenantA, ActorA, new FixedClock(start), retentionDays: 30)
            .RecordAsync(Entry());
        await CreateService(dbContext, TenantA, ActorA, new FixedClock(start.AddDays(20)), retentionDays: 30)
            .RecordAsync(Entry("attach"));

        var purged = await CreateService(
                dbContext, TenantA, ActorA, new FixedClock(start.AddDays(31)), retentionDays: 30)
            .PurgeExpiredAsync();

        Assert.Equal(1, purged);
        var remaining = Assert.Single(await dbContext.EInvoiceValidationRecords.ToArrayAsync());
        Assert.Equal("attach", remaining.Operation);
    }

    [Fact]
    public async Task PurgeExpiredAsync_KeepsARecordThatALaterRevalidationStillCites()
    {
        await using var dbContext = CreateContext();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var original = await CreateService(dbContext, TenantA, ActorA, new FixedClock(start), retentionDays: 30)
            .RecordAsync(Entry());
        await CreateService(dbContext, TenantA, ActorA, new FixedClock(start.AddDays(10)), retentionDays: 3650)
            .RecordAsync(Entry("revalidate") with { RevalidatesRecordId = original.Id });

        var purged = await CreateService(
                dbContext, TenantA, ActorA, new FixedClock(start.AddDays(31)), retentionDays: 30)
            .PurgeExpiredAsync();

        Assert.Equal(0, purged);
        Assert.Equal(2, await dbContext.EInvoiceValidationRecords.CountAsync());
    }

    [Fact]
    public async Task RecordAsync_KeepsBothVerdictsWhenAnArtifactSetChanges()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext, TenantA, ActorA);
        var first = await service.RecordAsync(Entry());

        var second = await service.RecordAsync(Entry("revalidate", Report(
            conformanceStatus: "invalid",
            isConformanceValidated: true,
            artifactSetId: "kosit-xrechnung-3.0.2",
            artifactSetSha256: new string('d', 64))) with { RevalidatesRecordId = first.Id });

        var history = await service.GetHistoryAsync(Sha256(InvoiceXml), 50);
        Assert.Equal(2, history.Count);
        var retainedOriginal = Assert.Single(history, item => item.Id == first.Id);
        Assert.Equal("notEvaluated", retainedOriginal.ConformanceStatus);
        Assert.Null(retainedOriginal.ValidatorArtifactSetId);
        Assert.Equal("invalid", Assert.Single(history, item => item.Id == second.Id).ConformanceStatus);
        Assert.Equal(first.Id, second.RevalidatesRecordId);
    }

    // ---------------------------------------------------------------- endpoints

    [Fact]
    public async Task Validate_RetainsItsVerdictAndTheHistoryEndpointReturnsIt()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);

        await controller.Validate(
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC", CancellationToken.None);
        var history = await controller.Validations(null, null, CancellationToken.None);

        var records = Assert.IsAssignableFrom<IReadOnlyList<PxaEInvoiceValidationEvidence>>(
            Assert.IsType<OkObjectResult>(history.Result).Value);
        var record = Assert.Single(records);
        Assert.Equal("validate", record.Operation);
        Assert.Equal(Sha256(InvoiceXml), record.DocumentSha256);
        Assert.Equal("notEvaluated", record.ConformanceStatus);
    }

    [Fact]
    public async Task Validations_FiltersByDocumentChecksumAndRejectsAMalformedFilter()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        await controller.Validate(
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC", CancellationToken.None);

        var matching = await controller.Validations(Sha256(InvoiceXml), null, CancellationToken.None);
        var other = await controller.Validations(new string('e', 64), null, CancellationToken.None);
        var malformed = await controller.Validations("nope", null, CancellationToken.None);

        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PxaEInvoiceValidationEvidence>>(
            Assert.IsType<OkObjectResult>(matching.Result).Value));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<PxaEInvoiceValidationEvidence>>(
            Assert.IsType<OkObjectResult>(other.Result).Value));
        Assert.Equal("PXAEINVAPI030", Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(malformed.Result).Value).Extensions["code"]);
    }

    [Fact]
    public async Task ValidationRecord_ReturnsNotFoundForAnUnknownIdentifier()
    {
        await using var dbContext = CreateContext();

        var result = await CreateController(dbContext).ValidationRecord(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("PXAEINVAPI031", Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(result.Result).Value).Extensions["code"]);
    }

    [Fact]
    public async Task ExportValidations_ReturnsADownloadableMachineReadableBundle()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        await controller.Validate(
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC", CancellationToken.None);

        var result = await controller.ExportValidations(null, null, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("pxa-e-invoice-validation-evidence.json", file.FileDownloadName);
        var json = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("pxa.e-invoice.validation-evidence/1", json, StringComparison.Ordinal);
        Assert.Contains(Sha256(InvoiceXml), json, StringComparison.Ordinal);
        Assert.Contains("\"conformanceStatus\": \"notEvaluated\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revalidate_StoresANewVerdictWithoutTouchingTheStoredOne()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        await controller.Validate(
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC", CancellationToken.None);
        var stored = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PxaEInvoiceValidationEvidence>>(
            Assert.IsType<OkObjectResult>(
                (await controller.Validations(null, null, CancellationToken.None)).Result).Value));

        var result = await controller.Revalidate(stored.Id, Xml(), CancellationToken.None);

        var response = Assert.IsType<PxaEInvoiceRevalidationResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(stored.Id, response.Previous.Id);
        Assert.NotEqual(stored.Id, response.Current.Id);
        Assert.Equal(stored.Id, response.Current.RevalidatesRecordId);
        Assert.Equal("revalidate", response.Current.Operation);
        Assert.False(response.VerdictChanged);
        Assert.False(response.ArtifactSetChanged);
        Assert.Equal(2, await dbContext.EInvoiceValidationRecords.CountAsync());
    }

    [Fact]
    public async Task Revalidate_RefusesADocumentWhoseChecksumDoesNotMatchTheRecord()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        await controller.Validate(
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC", CancellationToken.None);
        var stored = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PxaEInvoiceValidationEvidence>>(
            Assert.IsType<OkObjectResult>(
                (await controller.Validations(null, null, CancellationToken.None)).Result).Value));

        var result = await controller.Revalidate(
            stored.Id,
            Xml(Encoding.UTF8.GetBytes("<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\" />")),
            CancellationToken.None);

        Assert.Equal("PXAEINVAPI032", Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(result.Result).Value).Extensions["code"]);
        Assert.Equal(1, await dbContext.EInvoiceValidationRecords.CountAsync());
    }

    [Fact]
    public async Task Attach_RetainsTheVerdictOfTheInvoiceItEmbedded()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);

        var attached = await controller.Attach(
            PdfFile(), Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, allowInconsistentPdf: true,
            preserveUnrecognized: false, attachmentFileName: null,
            CancellationToken.None);

        Assert.IsType<FileContentResult>(attached);
        var record = Assert.Single(await dbContext.EInvoiceValidationRecords.ToArrayAsync());
        Assert.Equal("attach", record.Operation);
        Assert.Equal(Sha256(InvoiceXml), record.DocumentSha256);
        Assert.NotNull(record.ContainerSha256);
        Assert.Equal(record.Id.ToString(), controller.Response.Headers["PXA-Validation-Record"]);
    }

    [Fact]
    public async Task Attach_RetainsNoVerdictForPreservedUnknownXml()
    {
        await using var dbContext = CreateContext();

        var attached = await CreateController(dbContext).Attach(
            PdfFile(), Xml("<vendor:Legacy xmlns:vendor=\"urn:example:1\"/>"u8.ToArray()),
            null, null, null, false, false, false, false,
            preserveUnrecognized: true, attachmentFileName: null,
            CancellationToken.None);

        Assert.IsType<FileContentResult>(attached);
        Assert.Empty(await dbContext.EInvoiceValidationRecords.ToArrayAsync());
    }

    [Fact]
    public async Task Evidence_endpoints_fail_closed_when_no_store_is_configured()
    {
        var controller = new PdfEInvoicesController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var history = await controller.Validations(null, null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(history.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("PXAEINVAPI034", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Theory]
    [InlineData("/api/pxa/v1/pdf/e-invoices/validations", "GET", PxaDesignerPermissions.PdfEInvoicesRead)]
    [InlineData("/api/pxa/v1/pdf/e-invoices/validations/export", "GET", PxaDesignerPermissions.PdfEInvoicesRead)]
    [InlineData(
        "/api/pxa/v1/pdf/e-invoices/validations/6f1c1a4e-2f8a-4b5c-9c0d-1e2f3a4b5c6d",
        "GET",
        PxaDesignerPermissions.PdfEInvoicesRead)]
    [InlineData(
        "/api/pxa/v1/pdf/e-invoices/validations/6f1c1a4e-2f8a-4b5c-9c0d-1e2f3a4b5c6d/revalidate",
        "POST",
        PxaDesignerPermissions.PdfEInvoicesValidate)]
    public void Evidence_routes_resolve_to_their_permission(string path, string method, string expected)
    {
        Assert.Equal(expected, PxaDesignerPermissionResolver.Resolve(new PathString(path), method));
        Assert.Equal("generator", PxaProductAccessMiddleware.ResolveCapability(path));
    }

    // ---------------------------------------------------------------- helpers

    private static PxaEInvoiceValidationEvidenceEntry Entry(
        string operation = "validate",
        PxaEInvoiceValidationReport? report = null) => new(
        operation,
        Sha256(InvoiceXml),
        null,
        new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "BASIC"
        },
        report ?? Report());

    private static PxaEInvoiceValidationReport Report(
        string conformanceStatus = "notEvaluated",
        bool isConformanceValidated = false,
        string? artifactSetId = null,
        string? artifactSetSha256 = null) => new(
        true,
        isConformanceValidated,
        "CII",
        "urn:factur-x.eu:1p0:basic",
        [new("PXAEINV000", "information", "Structural recognition succeeded.")],
        conformanceStatus,
        artifactSetId,
        artifactSetSha256);

    private static PxaEInvoiceValidationEvidenceService CreateService(
        PxaDbContext dbContext,
        Guid organizationId,
        Guid actorUserId,
        TimeProvider? clock = null,
        int retentionDays = 3_650) =>
        new(
            dbContext,
            new TestTenantContext(actorUserId, organizationId),
            Options.Create(new PxaEInvoiceEvidenceOptions { RetentionDays = retentionDays }),
            clock);

    private static PdfEInvoicesController CreateController(PxaDbContext dbContext) =>
        new(evidence: CreateService(dbContext, TenantA, ActorA))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static PxaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase($"e-invoice-evidence-{Guid.NewGuid():N}")
            .Options);

    private static string Sha256(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

    private static FormFile Xml(byte[]? bytes = null) =>
        File(bytes ?? InvoiceXml, "factur-x.xml", "application/xml");

    private static FormFile PdfFile()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PXA.Pdf.PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText(
            "Statement", 40, 40, new PXA.Pdf.PdfDrawTextOptions { Language = "en" });
        return File(
            document.ToBytes(new PXA.Pdf.PdfSaveOptions
            {
                ComplianceProfile = PXA.Pdf.PdfComplianceProfile.PdfA3B
            }),
            "statement.pdf",
            "application/pdf");
    }

    private static FormFile File(byte[] bytes, string name, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestTenantContext(Guid? userId, Guid? organizationId) : IPxaTenantContext
    {
        public Guid? UserId { get; } = userId;
        public Guid? OrganizationId { get; } = organizationId;
    }
}
