using PVAD.Core.Contracts;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadExportBrandingTests
{
    [Fact]
    public void NewPdf_UsesCurrentCreatorWithoutRewritingCustomerMetadata()
    {
        var design = new DesignExportDto
        {
            Name = "Customer PXA document",
            PageSettings = new PageSettingsDto
            {
                Metadata = new PdfMetadataDto
                {
                    Title = "Canvas and PXA customer archive",
                    Author = "PXA customer",
                    Subject = "Original Canvas documents",
                    Keywords = "Canvas,PXA"
                }
            },
            Pages = [new PageDto { Id = "page-1", Elements = [] }]
        };

        var document = DesignJsonMapper.MapToPdfDocument(design);

        Assert.Equal("PVAD PDF Renderer", document.Info.Creator);
        Assert.Equal(design.PageSettings.Metadata.Title, document.Info.Title);
        Assert.Equal(design.PageSettings.Metadata.Author, document.Info.Author);
        Assert.Equal(design.PageSettings.Metadata.Subject, document.Info.Subject);
        Assert.Equal(design.PageSettings.Metadata.Keywords, document.Info.Keywords);
    }
}
