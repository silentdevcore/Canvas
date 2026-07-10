using PXA.Core.Abstractions;
using PXA.Core.Contracts;
using PXA.Core.Primitives;

namespace PXA.Core.Tests;

public sealed class CoreBehaviorPromotionTests
{
    [Fact]
    public void A1Reference_ConvertsBetweenIndicesAndA1Notation()
    {
        Assert.Equal("A", A1Reference.ColumnName(0));
        Assert.Equal("AA", A1Reference.ColumnName(26));
        Assert.Equal(27, A1Reference.ColumnIndex("AB"));
        Assert.Equal("C4", A1Reference.ToA1(3, 2));
        Assert.Equal((Row: 4, Col: 27), A1Reference.Parse("$AB$5"));
    }

    [Fact]
    public async Task ExpressionEvaluator_UsesPxaExpressionEngine()
    {
        IExpressionEvaluator evaluator = new ExpressionEvaluator();

        var result = await evaluator.EvaluateAsync(
            "$concat(First, \" \", Last, \" = \", Qty * Price)",
            new Dictionary<string, object>
            {
                ["First"] = "Ada",
                ["Last"] = "Lovelace",
                ["Qty"] = 3d,
                ["Price"] = 4d,
            });

        Assert.True(result.IsValid);
        Assert.Equal("Ada Lovelace = 12", result.Value);
    }

    [Fact]
    public async Task ExpressionEvaluator_RejectsDangerousPatterns()
    {
        IExpressionEvaluator evaluator = new ExpressionEvaluator();

        var result = await evaluator.EvaluateAsync("eval(\"x\")", []);

        Assert.False(result.IsValid);
        Assert.Equal("Expression contains potentially dangerous operations", result.Error);
    }

    [Fact]
    public void DesignLayoutPlanner_BuildsPxaPagesAndExpandsRepeats()
    {
        var design = new DesignExportDto
        {
            Id = "design-1",
            Name = "Repeat",
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "row",
                            Type = "text",
                            X = 10,
                            Y = 20,
                            Height = 12,
                            Repeat = new RepeatDto { DataPath = "Rows", TemplateId = "row" },
                            Content = "{{Name}}",
                        },
                    ],
                },
            ],
            PageSettings = new PageSettingsDto
            {
                CustomProperties =
                [
                    new CustomDocumentPropertyDto
                    {
                        Name = "Rows",
                        Value = """[{"Name":"One"},{"Name":"Two"}]""",
                    },
                ],
            },
        };

        var planned = DesignLayoutPlanner.BuildPages(design);

        Assert.Single(planned);
        Assert.Equal(["row__repeat_0", "row__repeat_1"], planned[0].Elements.Select(e => e.Id));
        Assert.Equal("One", planned[0].Elements[0].Content);
        Assert.Equal("Two", planned[0].Elements[1].Content);
        Assert.All(planned[0].Elements, element => Assert.Null(element.Repeat));
    }

    [Fact]
    public void ValueFormatter_FormatsCommonPxaValues()
    {
        IValueFormatter formatter = new ValueFormatter();

        Assert.Equal("HELLO", formatter.Format("hello", "uppercase"));
        Assert.Equal("Hello World", formatter.Format("hello world", "titlecase"));
        Assert.Equal("hello...", formatter.Format("hello world", "truncate:8:..."));
    }
}
