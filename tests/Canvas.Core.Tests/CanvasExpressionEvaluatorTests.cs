using Canvas.Core.Primitives;

namespace Canvas.Core.Tests;

public class CanvasExpressionEvaluatorTests
{
    private static object? Eval(string expr, Dictionary<string, object?> data)
    {
        Assert.True(CanvasExpressionEvaluator.TryEvaluate(expr, data, out var v), $"should evaluate: {expr}");
        return v;
    }

    [Fact]
    public void Arithmetic_And_Identifiers()
    {
        Assert.Equal(12d, Eval("Qty * Price", new() { ["Qty"] = 3d, ["Price"] = 4d }));
        Assert.Equal(7d, Eval("a + b", new() { ["a"] = 3d, ["b"] = 4d }));
    }

    [Fact]
    public void Concat_StringPlus_And_Helper()
    {
        var data = new Dictionary<string, object?> { ["First"] = "Ada", ["Last"] = "Lovelace" };
        Assert.Equal("Ada Lovelace", Eval("$concat(First, \" \", Last)", data));
        Assert.Equal("Ada Lovelace", Eval("First + \" \" + Last", data));   // '+' concats non-numeric
    }

    [Fact]
    public void Iif_With_Comparison_And_Nesting()
    {
        Assert.Equal("Yes", Eval("$iif(Paid == true, \"Yes\", \"No\")", new() { ["Paid"] = true }));
        Assert.Equal("No", Eval("$iif(Paid == true, \"Yes\", \"No\")", new() { ["Paid"] = false }));
        Assert.Equal("x5", Eval("$iif(Qty == 0, \"n/a\", $concat(\"x\", Qty))", new() { ["Qty"] = 5d }));
    }

    [Fact]
    public void Logical_Helpers()
    {
        Assert.Equal(true, Eval("$and(A == 1, B == 2)", new() { ["A"] = 1d, ["B"] = 2d }));
        Assert.Equal(true, Eval("$or(A == 1, B == 2)", new() { ["A"] = 0d, ["B"] = 2d }));
        Assert.Equal(false, Eval("$not(A == 1)", new() { ["A"] = 1d }));
    }

    [Fact]
    public void UnknownFunction_Or_Malformed_ReturnsFalse()
    {
        Assert.False(CanvasExpressionEvaluator.TryEvaluate("Sum(Total)", new Dictionary<string, object?> { ["Total"] = 5d }, out _));
        Assert.False(CanvasExpressionEvaluator.TryEvaluate("a +", new Dictionary<string, object?>(), out _));
    }

    private static Dictionary<string, object?> Dataset() => new()
    {
        ["Orders"] = new List<object?>
        {
            new Dictionary<string, object?> { ["Total"] = 10d, ["Name"] = "A" },
            new Dictionary<string, object?> { ["Total"] = 20L, ["Name"] = "B" },   // long (JSON integer)
            new Dictionary<string, object?> { ["Total"] = 30d, ["Name"] = "C" },
        }
    };

    [Fact]
    public void Aggregates_Over_Dataset()
    {
        var d = Dataset();
        Assert.Equal(60d, Eval("$sum(Orders, \"Total\")", d));     // includes the long row
        Assert.Equal(20d, Eval("$avg(Orders, \"Total\")", d));
        Assert.Equal(3d, Eval("$count(Orders)", d));
        Assert.Equal(3d, Eval("$count(Orders, \"Total\")", d));
        Assert.Equal(10d, Eval("$min(Orders, \"Total\")", d));
        Assert.Equal(30d, Eval("$max(Orders, \"Total\")", d));
        Assert.Equal("A", Eval("$first(Orders, \"Name\")", d));
        Assert.Equal("C", Eval("$last(Orders, \"Name\")", d));
    }

    [Fact]
    public void Aggregate_In_Concat_And_Empty_Dataset()
    {
        var d = Dataset();
        Assert.Equal("Total: 60", Eval("$concat(\"Total: \", $sum(Orders, \"Total\"))", d));
        var empty = new Dictionary<string, object?> { ["Orders"] = new List<object?>() };
        Assert.Equal(0d, Eval("$sum(Orders, \"Total\")", empty));
        Assert.Equal(0d, Eval("$avg(Orders, \"Total\")", empty));
    }

    [Fact]
    public void Aggregate_NonDataset_Arg_ReturnsFalse()
    {
        Assert.False(CanvasExpressionEvaluator.TryEvaluate(
            "$sum(Total, \"x\")", new Dictionary<string, object?> { ["Total"] = 5d }, out _));
    }

    [Fact]
    public void FormatValue_TrimsIntegralDoubles()
    {
        Assert.Equal("12", CanvasExpressionEvaluator.FormatValue(12d));
        Assert.Equal("12.5", CanvasExpressionEvaluator.FormatValue(12.5d));
        Assert.Equal("", CanvasExpressionEvaluator.FormatValue(null));
    }
}
