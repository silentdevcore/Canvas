using PXA.Application.UseCases;
using PXA.Core.Contracts;

namespace PXA.Application.Tests;

public sealed class DesignUseCaseFacadeTests
{
    [Fact]
    public void CloneTemplate_UsesPxaCoreContracts()
    {
        var design = BuildDesign();

        var clone = new CloneTemplateUseCase().Execute(new CloneDesignRequest
        {
            Design = design,
            NewName = "Copy",
        });

        Assert.IsType<DesignExportDto>(clone);
        Assert.Equal("Copy", clone.Name);
        Assert.NotEqual(design.Id, clone.Id);
        Assert.NotEqual(design.Pages[0].Elements[0].Id, clone.Pages[0].Elements[0].Id);
        Assert.Equal("Hello PXA", clone.Pages[0].Elements[0].Content);
    }

    [Fact]
    public void ExtractPages_UsesPxaCoreContracts()
    {
        var design = BuildDesign();

        var extracted = new ExtractPagesUseCase().Execute(new ExtractPagesRequest
        {
            Design = design,
            PageNumbers = [2],
            NewName = "Page 2",
        });

        Assert.IsType<DesignExportDto>(extracted);
        Assert.Equal("Page 2", extracted.Name);
        Assert.Single(extracted.Pages);
        Assert.Equal("page-2", extracted.Pages[0].Id);
    }

    [Fact]
    public void FindAndReplace_UsesPxaCoreContracts()
    {
        var design = BuildDesign();

        var result = new FindAndReplaceUseCase().Execute(new FindAndReplaceRequest
        {
            Design = design,
            Find = "PXA",
            Replace = "Power Dox Automation",
        });

        Assert.IsType<DesignExportDto>(result.Design);
        Assert.Equal(1, result.ReplacementCount);
        Assert.Equal("Hello Power Dox Automation", result.Design.Pages[0].Elements[0].Content);
        Assert.Contains("text-1", result.AffectedElementIds);
    }

    private static DesignExportDto BuildDesign() => new()
    {
        Id = "design-1",
        Name = "Source",
        Pages =
        [
            new PageDto
            {
                Id = "page-1",
                Elements =
                [
                    new ElementDto
                    {
                        Id = "text-1",
                        Type = "text",
                        Content = "Hello PXA",
                        Width = 100,
                        Height = 20,
                    }
                ],
            },
            new PageDto
            {
                Id = "page-2",
                Elements =
                [
                    new ElementDto
                    {
                        Id = "text-2",
                        Type = "text",
                        Content = "Second page",
                        Width = 100,
                        Height = 20,
                    }
                ],
            },
        ],
    };
}
