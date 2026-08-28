using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.Pdf;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PdfSignaturesControllerTests
{
    [Fact]
    public async Task Sign_ReturnsSignedPdfForValidPkcs12()
    {
        var pdf = CreatePdf();
        var certificate = CreatePkcs12("test-password");
        var controller = CreateController();

        var response = await controller.Sign(
            new PdfSignatureSignRequest
            {
                Pdf = CreateFile(pdf, "pdf", "contract.pdf", "application/pdf"),
                Certificate = CreateFile(certificate, "certificate", "signer.pfx", "application/x-pkcs12"),
                CertificatePassword = "test-password",
                FieldName = "Approval",
                Reason = "Approved"
            },
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(response);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("contract.signed.pdf", file.FileDownloadName);
        Assert.True(file.FileContents.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        await using var stream = new MemoryStream(file.FileContents);
        var trust = await new PdfTrustInspector().InspectAsync(stream);
        Assert.Single(trust.Signatures);
        Assert.Equal("B-B", controller.Response.Headers["X-PXA-PAdES-Profile"]);
    }

    [Fact]
    public async Task Sign_ReturnsStableProblemForInvalidCertificatePassword()
    {
        var controller = CreateController();
        var response = await controller.Sign(
            new PdfSignatureSignRequest
            {
                Pdf = CreateFile(CreatePdf(), "pdf", "document.pdf", "application/pdf"),
                Certificate = CreateFile(CreatePkcs12("correct"), "certificate", "signer.pfx", "application/x-pkcs12"),
                CertificatePassword = "wrong"
            },
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("PXAPDFTRUSTAPI104", details.Extensions["code"]);
    }

    [Fact]
    public async Task Sign_CreatesVisibleAppearanceWhenRequested()
    {
        var controller = CreateController();
        var response = await controller.Sign(
            new PdfSignatureSignRequest
            {
                Pdf = CreateFile(CreatePdf(), "pdf", "visible.pdf", "application/pdf"),
                Certificate = CreateFile(CreatePkcs12("test-password"), "certificate", "signer.pfx", "application/x-pkcs12"),
                CertificatePassword = "test-password",
                Visible = true,
                PageNumber = 1,
                X = 48,
                Y = 52,
                Width = 210,
                Height = 70,
                AppearanceTitle = "Release signature"
            },
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(response);
        var pdfText = Encoding.Latin1.GetString(file.FileContents);
        Assert.Contains("/Subtype /Widget", pdfText, StringComparison.Ordinal);
        Assert.Contains("/AP", pdfText, StringComparison.Ordinal);
        Assert.Contains("Release signature", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_ReturnsServiceUnavailableWhenBaselineTIsNotConfigured()
    {
        var controller = CreateController();
        var response = await controller.Sign(
            new PdfSignatureSignRequest
            {
                Pdf = CreateFile(CreatePdf(), "pdf", "timestamped.pdf", "application/pdf"),
                Certificate = CreateFile(CreatePkcs12("test-password"), "certificate", "signer.pfx", "application/x-pkcs12"),
                CertificatePassword = "test-password",
                Profile = PdfPadesProfile.BaselineT
            },
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("PXAPDFTRUSTAPI105", details.Extensions["code"]);
    }

    [Fact]
    public async Task Validate_ReturnsCryptographicAndCertificateStatuses()
    {
        var pdf = CreatePdf();
        var certificate = CreatePkcs12("test-password");
        var controller = CreateController();
        var signed = Assert.IsType<FileContentResult>(await controller.Sign(
            new PdfSignatureSignRequest
            {
                Pdf = CreateFile(pdf, "pdf", "contract.pdf", "application/pdf"),
                Certificate = CreateFile(certificate, "certificate", "signer.pfx", "application/x-pkcs12"),
                CertificatePassword = "test-password"
            },
            CancellationToken.None));

        var response = await controller.Validate(
            CreateFile(signed.FileContents, "file", "contract.signed.pdf", "application/pdf"),
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var report = Assert.IsType<PdfTrustReport>(ok.Value);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Valid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.CertificateStatus);
    }

    [Fact]
    public void Controller_UsesSeparateSignaturePermissionsAndRateLimit()
    {
        var type = typeof(PdfSignaturesController);
        var rateLimit = Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Cast<EnableRateLimitingAttribute>());
        var signAuthorization = Assert.Single(typeof(PdfSignaturesController)
            .GetMethod(nameof(PdfSignaturesController.Sign))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());
        var validateAuthorization = Assert.Single(typeof(PdfSignaturesController)
            .GetMethod(nameof(PdfSignaturesController.Validate))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(PxaDesignerPermissions.PdfSignaturesSign, signAuthorization.Policy);
        Assert.Equal(PxaDesignerPermissions.PdfSignaturesValidate, validateAuthorization.Policy);
        Assert.Equal("designer-code", rateLimit.PolicyName);
        Assert.Contains(PxaDesignerPermissions.PdfSignaturesSign, PxaRoles.Permissions[PxaRoles.Manager]);
        Assert.Contains(PxaDesignerPermissions.PdfSignaturesSign, PxaRoles.Permissions[PxaRoles.Editor]);
        Assert.DoesNotContain(PxaDesignerPermissions.PdfSignaturesSign, PxaRoles.Permissions[PxaRoles.Viewer]);
        Assert.Contains(PxaDesignerPermissions.PdfSignaturesValidate, PxaRoles.Permissions[PxaRoles.Viewer]);
        Assert.Equal(
            PxaDesignerPermissions.PdfSignaturesSign,
            PxaDesignerPermissionResolver.Resolve("/api/pxa/v1/pdf/signatures/sign", "POST"));
        Assert.Equal(
            PxaDesignerPermissions.PdfSignaturesValidate,
            PxaDesignerPermissionResolver.Resolve("/api/pxa/v1/pdf/signatures/validate", "POST"));
    }

    private static PdfSignaturesController CreateController() => new(
        new PdfPadesSigningService(),
        new PdfTrustInspector(),
        new EmptyTimestampAuthorityFactory(),
        new TestTrustPolicyProvider())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private sealed class EmptyTimestampAuthorityFactory : IPxaPdfTimestampAuthorityFactory
    {
        public IPdfTimestampAuthority? Create() => null;
    }

    private sealed class TestTrustPolicyProvider : IPxaPdfTrustPolicyProvider
    {
        public PdfSignatureValidationPolicy Policy { get; } = new();
    }

    private static FormFile CreateFile(byte[] bytes, string fieldName, string fileName, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, fieldName, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("PXA API signing fixture", 40, 40);
        return document.ToBytes();
    }

    private static byte[] CreatePkcs12(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA API Signing Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(2));
        return certificate.Export(X509ContentType.Pkcs12, password);
    }
}
