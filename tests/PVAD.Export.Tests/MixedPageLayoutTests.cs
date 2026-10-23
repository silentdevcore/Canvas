using PVAD.WebApi.Infrastructure;

namespace PVAD.Export.Tests;

public sealed class MixedPageLayoutTests
{
    [Fact]
    public void PdfRenderer_UsesEffectiveGeometryAndBleedForEveryPage()
    {
        var design = new DesignExportDto
        {
            Id = "mixed",
            Name = "Mixed page PDF",
            PageSettings = new PageSettingsDto { Width = 595, Height = 842, Orientation = "portrait" },
            Pages =
            [
                new PageDto { Id = "a4-portrait" },
                new PageDto
                {
                    Id = "a4-landscape",
                    Layout = new PageLayoutDto { Width = 842, Height = 595, Orientation = "landscape" },
                },
                new PageDto
                {
                    Id = "custom",
                    Layout = new PageLayoutDto { Width = 400, Height = 500, Orientation = "portrait", BleedSize = 10 },
                },
            ],
        };

        var pdf = DesignJsonMapper.MapToPdfDocument(design);

        Assert.Collection(pdf.Pages,
            page => Assert.Equal((595d, 842d), (page.Width, page.Height)),
            page => Assert.Equal((842d, 595d), (page.Width, page.Height)),
            page =>
            {
                Assert.Equal((400d, 500d), (page.Width, page.Height));
                Assert.Equal(10, page.TrimBoxLowerLeft!.Value.X);
                Assert.Equal(390, page.TrimBoxUpperRight!.Value.X);
                Assert.Equal(400, page.BleedBoxUpperRight!.Value.X);
            });
    }
}
