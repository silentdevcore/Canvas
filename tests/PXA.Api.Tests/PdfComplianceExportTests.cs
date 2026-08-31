using PXA.Core.Contracts;
using PXA.Pdf;
using PXA.WebApi.Infrastructure;

namespace PXA.Api.Tests;

public sealed class PdfComplianceExportTests
{
    [Fact]
    public void BuildPdfSaveOptions_MapsPdfA3bComplianceProfile()
    {
        var design = new DesignExportDto
        {
            PageSettings = new PageSettingsDto { ComplianceProfile = "PdfA3B" }
        };

        var options = DesignJsonMapper.BuildSaveOptions(design);

        Assert.NotNull(options);
        Assert.Equal(PdfComplianceProfile.PdfA3B, options.ComplianceProfile);
    }

    [Fact]
    public void BuildSaveOptions_MapsPdfA2bProfile()
    {
        var design = new DesignExportDto
        {
            PageSettings = new PageSettingsDto { ComplianceProfile = "PdfA2B" }
        };

        var options = DesignJsonMapper.BuildSaveOptions(design);

        Assert.NotNull(options);
        Assert.Equal(PdfComplianceProfile.PdfA2B, options.ComplianceProfile);
        Assert.Null(options.Encryption);
    }

    [Fact]
    public void BuildSaveOptions_RejectsPdfAWithEncryption()
    {
        var design = new DesignExportDto
        {
            PageSettings = new PageSettingsDto
            {
                ComplianceProfile = "PdfA2B",
                Encryption = new PdfEncryptionDto
                {
                    Enabled = true,
                    Algorithm = "Aes256",
                    UserPassword = "reader"
                }
            }
        };

        var exception = Assert.Throws<InvalidDataException>(() => DesignJsonMapper.BuildSaveOptions(design));

        Assert.Contains("cannot be enabled together", exception.Message);
    }
}
