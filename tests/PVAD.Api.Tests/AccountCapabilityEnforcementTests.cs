using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.FileImporter;
using PVAD.Infrastructure.Converters;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Licensing;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

public sealed class AccountCapabilityEnforcementTests
{
    [Theory]
    [InlineData("pdf")]
    [InlineData("svg")]
    [InlineData("image")]
    public async Task DirectFileImporterRejectsPrivateInputBeforeReadingAnyBytes(string format)
    {
        using var scope = PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper);
        using var stream = new ThrowOnReadStream();
        var error = await Assert.ThrowsAsync<PvadLicenseException>(async () =>
            await FileImporterRegistry.Create(format).ImportAsync(stream, "synthetic"));
        Assert.Equal("PVAD_ACCOUNT_CAPABILITY_DENIED", error.Code);
    }

    [Fact]
    public void PrivateCanImportMarkdownAndExportOdtAndPdf()
    {
        using var scope = PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("# Synthetic document"));
        var design = MarkdownFileImporter.Import(stream, "synthetic");
        Assert.NotEmpty(new OdtDocumentExporter().Export(design));
        var pdf = PVAD.Generator.Pdf.CreateDocument();
        pdf.AddPage(100, 100);
        Assert.NotEmpty(pdf.ToBytes());
    }

    [Fact]
    public void DirectImageAndTextExportersRejectPrivateOutputBeforeInputValidation()
    {
        using var scope = PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper);
        AssertDenied(() => new ImageDocumentExporter().Export(null!));
        AssertDenied(() => new JpegDocumentExporter().Export(null!));
        AssertDenied(() => new TiffDocumentExporter().Export(null!));
        AssertDenied(() => new SvgDocumentExporter().Export(null!));
        AssertDenied(() => new HtmlDocumentExporter().Export(null!));
        AssertDenied(() => new XmlDocumentExporter().Export(null!));
        AssertDenied(() => new MarkdownDocumentExporter().Export(null!));
        AssertDenied(() => new CsvDocumentExporter().Export(null!));
    }

    [Fact]
    public void SpreadsheetCannotBypassCsvRestrictionWithAnEmptySheetOrSave()
    {
        using var scope = PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper);
        AssertDenied(() => CsvSheetIo.ToCsv(new SheetDto()));
        AssertDenied(() => CsvSheetIo.ToCsv(new SheetDto(), '\t'));
        var path = Path.Combine(Path.GetTempPath(), "pvad-denied-" + Guid.NewGuid().ToString("N") + ".csv");
        var workbook = new PvadWorkbook();
        AssertDenied(() => workbook.Save(path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void PrivateCannotCallStandaloneEInvoiceUtilities()
    {
        using var scope = PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper);
        AssertDenied(() => PVAD.Generator.EInvoice.GenerateXml(null!, null!));
        AssertDenied(() => PVAD.Generator.EInvoice.Validate(ReadOnlyMemory<byte>.Empty, null!));
        AssertDenied(() => PVAD.Generator.EInvoice.Recognize(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task RemovingAuthenticationCannotBypassAccountRestrictions()
    {
        await using var db = NewDatabase();
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/export";
        http.Response.Body = new MemoryStream();
        var invoked = false;
        await new PvadAccountCapabilityMiddleware(_ => { invoked = true; return Task.CompletedTask; })
            .InvokeAsync(http, db, new Tenant(Guid.NewGuid()));
        Assert.False(invoked);
        Assert.Equal(401, http.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivateApiRequestIsDeniedRegardlessOfDesignerHeaderOrAdminRole(bool designerHeader)
    {
        await using var db = NewDatabase();
        var subscription = new OrganizationSubscription { OrganizationId = Guid.NewGuid(), AccountType = SubscriptionAccountType.IndividualDeveloper, Status = SubscriptionStatus.Active };
        db.OrganizationSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, PvadRoles.SystemAdministrator)], "synthetic"));
        http.Request.Path = "/api/document/import-pdf-engine";
        if (designerHeader) http.Request.Headers["X-PVAD-Application"] = "designer";
        http.Response.Body = new MemoryStream();
        var invoked = false;
        var middleware = new PvadAccountCapabilityMiddleware(_ => { invoked = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(http, db, new Tenant(subscription.OrganizationId));
        Assert.False(invoked);
        Assert.Equal(403, http.Response.StatusCode);
    }

    [Fact]
    public async Task MigrationRequiresNonDefaultUnexpiredAdminEntitlement()
    {
        await using var db = NewDatabase();
        var subscription = new OrganizationSubscription { OrganizationId = Guid.NewGuid(), AccountType = SubscriptionAccountType.Company };
        db.OrganizationSubscriptions.Add(subscription);
        var grant = new SubscriptionEntitlement { SubscriptionId = subscription.Id, Capability = "migration", Enabled = true };
        db.SubscriptionEntitlements.Add(grant);
        await db.SaveChangesAsync();
        Assert.Empty((await PvadAccountPolicyService.ReadAsync(db, subscription.OrganizationId, default))!.ApprovedMigrations);
        grant.Source = EntitlementSource.NegotiatedOverride;
        await db.SaveChangesAsync();
        Assert.NotEmpty((await PvadAccountPolicyService.ReadAsync(db, subscription.OrganizationId, default))!.ApprovedMigrations);
        grant.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        Assert.Empty((await PvadAccountPolicyService.ReadAsync(db, subscription.OrganizationId, default))!.ApprovedMigrations);
    }

    private static PvadDbContext NewDatabase() => new(new DbContextOptionsBuilder<PvadDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void AssertDenied(Action action) => Assert.Equal("PVAD_ACCOUNT_CAPABILITY_DENIED",
        Assert.Throws<PvadLicenseException>(action).Code);

    private sealed class Tenant(Guid organizationId) : IPvadTenantContext
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? OrganizationId => organizationId;
    }

    private sealed class ThrowOnReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Input was read before denial.");
        public override int Read(Span<byte> buffer) => throw new InvalidOperationException("Input was read before denial.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Input was read before denial.");
    }
}
