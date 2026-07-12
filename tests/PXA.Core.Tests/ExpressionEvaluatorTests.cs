using PXA.Core.Abstractions;
using PXA.Core.Primitives;

namespace PXA.Core.Tests;

// IExpressionEvaluator is now backed by the shared PxaExpressionEvaluator, so the TemplateExpander
// value/visibility path evaluates real PXA grammar (helpers, operators) — not the old regex stub.
public class ExpressionEvaluatorTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator();

    private static async Task<ExpressionResult> Eval(string expr, Dictionary<string, object> data) =>
        await Evaluator.EvaluateAsync(expr, data);

    [Fact]
    public async Task Concat_Helper_And_Arithmetic_Use_The_Real_Engine()
    {
        var concat = await Eval("$concat(First, \" \", Last)",
            new() { ["First"] = "Ada", ["Last"] = "Lovelace" });
        Assert.True(concat.IsValid);
        Assert.Equal("Ada Lovelace", concat.Value);

        var math = await Eval("Qty * Price", new() { ["Qty"] = 3d, ["Price"] = 4d });
        Assert.True(math.IsValid);
        Assert.Equal(12d, math.Value);
    }

    [Fact]
    public async Task Iif_And_Comparison()
    {
        Assert.Equal("Yes", (await Eval("$iif(Paid == true, \"Yes\", \"No\")", new() { ["Paid"] = true })).Value);
        Assert.Equal("No", (await Eval("$iif(Paid == true, \"Yes\", \"No\")", new() { ["Paid"] = false })).Value);

        var cmp = await Eval("A == B", new() { ["A"] = 1d, ["B"] = 1d });
        Assert.True(cmp.IsValid);
        Assert.Equal(true, cmp.Value);
    }

    [Fact]
    public async Task DangerousPattern_IsRejected()
    {
        var r = await Eval("eval(\"x\")", new());
        Assert.False(r.IsValid);
        Assert.Equal("Expression contains potentially dangerous operations", r.Error);
    }

    [Fact]
    public async Task Unparseable_Expression_IsInvalid()
    {
        var r = await Eval("a +", new());
        Assert.False(r.IsValid);
        Assert.Null(r.Value);
    }
}
