using PVAD.Core.Contracts;
using PVAD.Core.Primitives;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadExportBrandingTests
{
    [Fact]
    public void New_chart_pdf_writes_only_the_canonical_metadata_key()
    {
        var design = new DesignExportDto
        {
            Name = "Sales",
            Pages = [new PageDto
            {
                Id = "page-1", Elements = [new ElementDto
                {
                    Id = "chart-1", Type = "chart", X = 10, Y = 10, Width = 300, Height = 180,
                    Chart = new ChartDefinitionDto
                    {
                        Categories = ["A"],
                        Series = [new ChartSeriesDto { Id = "sales", Name = "Sales", Values = [5] }],
                    },
                }],
            }],
        };

        var document = DesignJsonMapper.MapToPdfDocument(design);

        Assert.True(document.Info.CustomProperties.ContainsKey(ChartMetadataCodec.PdfInfoKey));
        Assert.False(document.Info.CustomProperties.ContainsKey(ChartMetadataCodec.LegacyPdfInfoKey));
        Assert.True(ChartMetadataCodec.TryDecode(
            document.Info.CustomProperties[ChartMetadataCodec.PdfInfoKey], out var metadata));
        Assert.Equal("chart-1", Assert.Single(metadata.Charts).ElementId);
    }

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
