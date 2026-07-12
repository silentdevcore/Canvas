using PXA.Core.Primitives;

namespace PXA.Core.Tests;

public class CanvasExpressionEvaluatorTests
{
    private static object? Eval(string expr, Dictionary<string, object?> data)
    {
        Assert.True(PxaExpressionEvaluator.TryEvaluate(expr, data, out var v), $"should evaluate: {expr}");
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
    public void LogicalOperators_ShortCircuit()
    {
        // The right side is dangerous on its own (an aggregate over a non-dataset throws → invalid)…
        Assert.False(PxaExpressionEvaluator.TryEvaluate(
            "$count(Missing) > 0", new Dictionary<string, object?>(), out _));

        // …but && short-circuits when the left is false, and || when the left is true — the right side is
        // not evaluated, so the guard does not throw and the expression stays valid.
        Assert.Equal(false, Eval("Items != null && $count(Items) > 0", new Dictionary<string, object?>()));
        Assert.Equal(true, Eval("Total > 0 || $count(Missing) > 0", new() { ["Total"] = 5d }));

        // Both sides still evaluate when the left does not decide the result.
        Assert.Equal(true, Eval("A > 0 && B > 0", new() { ["A"] = 5d, ["B"] = 3d }));
        Assert.Equal(false, Eval("A > 0 && B > 0", new() { ["A"] = 5d, ["B"] = -1d }));
    }

    [Fact]
    public void UnknownFunction_Or_Malformed_ReturnsFalse()
    {
        Assert.False(PxaExpressionEvaluator.TryEvaluate("Sum(Total)", new Dictionary<string, object?> { ["Total"] = 5d }, out _));
        Assert.False(PxaExpressionEvaluator.TryEvaluate("a +", new Dictionary<string, object?>(), out _));
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
    public void Aggregate_Over_ComputedRowExpression()
    {
        // The aggregate's second argument may be a per-row sub-expression, not just a field name:
        // Sum(Qty*Price) / Sum(IIf(...)) translate to $sum(ds, "<expr>") and evaluate the expr per row.
        var d = Dataset();   // Total = 10, 20, 30
        Assert.Equal(120d, Eval("$sum(Orders, \"Total * 2\")", d));                  // (10 + 20 + 30) * 2
        Assert.Equal(50d, Eval("$sum(Orders, \"$iif(Total > 15, Total, 0)\")", d));  // 20 + 30 only
        Assert.Equal(60d, Eval("$sum(Orders, \"Total\")", d));                       // bare field still works
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
        Assert.False(PxaExpressionEvaluator.TryEvaluate(
            "$sum(Total, \"x\")", new Dictionary<string, object?> { ["Total"] = 5d }, out _));
    }

    [Fact]
    public void FormatValue_TrimsIntegralDoubles()
    {
        Assert.Equal("12", PxaExpressionEvaluator.FormatValue(12d));
        Assert.Equal("12.5", PxaExpressionEvaluator.FormatValue(12.5d));
        Assert.Equal("", PxaExpressionEvaluator.FormatValue(null));
    }
}
