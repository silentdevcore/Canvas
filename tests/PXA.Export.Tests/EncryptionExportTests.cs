using System.Text;
using PXA.Core.Contracts;
using PXA.Pdf;
using PXA.WebApi.Infrastructure;

namespace PXA.Export.Tests;

public sealed class EncryptionExportTests
{
    private static DesignExportDto DesignWithEncryption(PdfEncryptionDto? encryption)
    {
        return new DesignExportDto
        {
            Name = "Secret Doc",
            PageSettings = new PageSettingsDto
            {
                Width = 595,
                Height = 842,
                Encryption = encryption
            },
            Pages = new List<PageDto>
            {
                new()
                {
                    Elements = new List<ElementDto>
                    {
                        new() { Type = "text", X = 40, Y = 40, Content = "Confidential" }
                    }
                }
            }
        };
    }

    [Fact]
    public void BuildSaveOptions_ReturnsNull_WhenEncryptionDisabled()
    {
        var design = DesignWithEncryption(new PdfEncryptionDto { Enabled = false });

        Assert.Null(DesignJsonMapper.BuildSaveOptions(design));
    }

    [Fact]
    public void BuildSaveOptions_ReturnsNull_WhenNoEncryption()
    {
        Assert.Null(DesignJsonMapper.BuildSaveOptions(DesignWithEncryption(null)));
    }

    [Fact]
    public void BuildSaveOptions_MapsPasswordsAndPermissions()
    {
        var design = DesignWithEncryption(new PdfEncryptionDto
        {
            Enabled = true,
            UserPassword = "open",
            OwnerPassword = "admin",
            Algorithm = "Rc4_128",
            Permissions = new PdfEncryptionPermissionsDto
            {
                Print = true,
                Copy = true,
                Modify = false,
                Annotate = false,
                FillForms = false,
                ExtractAccessibility = false,
                Assemble = false,
                PrintHighResolution = false
            }
        });

        var options = DesignJsonMapper.BuildSaveOptions(design);

        Assert.NotNull(options);
        Assert.NotNull(options!.Encryption);
        Assert.Equal("open", options.Encryption!.UserPassword);
        Assert.Equal("admin", options.Encryption.OwnerPassword);
        Assert.Equal(PdfEncryptionAlgorithm.Rc4_128, options.Encryption.Algorithm);
        Assert.Equal(PdfPermissions.Print | PdfPermissions.Copy, options.Encryption.Permissions);
    }

    [Fact]
    public void Export_WithEncryption_ProducesEncryptedPdf()
    {
        var design = DesignWithEncryption(new PdfEncryptionDto
        {
            Enabled = true,
            UserPassword = "open",
            Algorithm = "Rc4_128"
        });

        var document = DesignJsonMapper.MapToPdfDocument(design);
        var bytes = document.ToBytes(DesignJsonMapper.BuildSaveOptions(design));

        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("/Encrypt", text);
        Assert.Contains("/Filter /Standard", text);
        Assert.Contains("/ID [<", text);
    }

    [Fact]
    public void Export_WithoutEncryption_ProducesPlainPdf()
    {
        var design = DesignWithEncryption(null);

        var document = DesignJsonMapper.MapToPdfDocument(design);
        var bytes = document.ToBytes(DesignJsonMapper.BuildSaveOptions(design));

        Assert.DoesNotContain("/Encrypt", Encoding.ASCII.GetString(bytes));
    }
}
