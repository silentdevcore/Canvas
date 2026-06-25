using Canvas.Migration.Abstractions;

namespace Canvas.Migration.Rdl.Tests;

public sealed class ExpressionTranslatorTests
{
    [Theory]
    [InlineData("=Fields!First.Value & \" \" & Fields!Last.Value", "$concat(First, \" \", Last)")]
    [InlineData("=IIf(Fields!Paid.Value, \"Yes\", \"No\")", "$iif(Paid, \"Yes\", \"No\")")]
    [InlineData("=Fields!Qty.Value * Fields!Price.Value", "Qty * Price")]
    [InlineData("=IIf(Fields!X.Value = 1, \"a\", \"b\")", "$iif(X == 1, \"a\", \"b\")")]
    [InlineData("=Fields!A.Value <> Fields!B.Value", "A != B")]
    [InlineData("=Fields!A.Value And Fields!B.Value", "$and(A, B)")]
    [InlineData("=Parameters!P.Value", "P")]
    [InlineData("=\"A & B\"", "\"A & B\"")]                       // '&' inside a string is not concat
    public void TranslateRdl_ProducesCanvasGrammar(string input, string expected)
        => Assert.Equal(expected, ExpressionTranslator.TranslateRdl(input));

    [Theory]
    [InlineData("[Qty] * [Price]", "Qty * Price")]
    [InlineData("Iif([Ok], 1, 0)", "$iif(Ok, 1, 0)")]
    [InlineData("[Ds.Field]", "Field")]
    [InlineData("[A] == [B]", "A == B")]
    public void TranslateDevExpress_ProducesCanvasGrammar(string input, string expected)
        => Assert.Equal(expected, ExpressionTranslator.TranslateDevExpress(input));

    [Fact]
    public void Translate_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(ExpressionTranslator.TranslateRdl(null));
        Assert.Null(ExpressionTranslator.TranslateRdl("   "));
        Assert.Null(ExpressionTranslator.TranslateDevExpress(""));
    }

    [Theory]
    [InlineData("=Sum(Fields!Total.Value)", "Orders", "$sum(Orders, \"Total\")")]
    [InlineData("=Count(Fields!Id.Value)", "Orders", "$count(Orders, \"Id\")")]
    [InlineData("=Avg(Fields!Price.Value)", "Orders", "$avg(Orders, \"Price\")")]
    [InlineData("=Max(Fields!Total.Value)", "Sales", "$max(Sales, \"Total\")")]
    public void TranslateRdl_Aggregates_WithDataset(string input, string dataSet, string expected)
        => Assert.Equal(expected, ExpressionTranslator.TranslateRdl(input, dataSet));

    [Fact]
    public void TranslateRdl_Aggregate_WithoutDataset_StaysUntranslated()
    {
        // No dataset → aggregate not translatable; the bare-call form comes back (caller keeps the raw).
        Assert.Equal("Sum(Total)", ExpressionTranslator.TranslateRdl("=Sum(Fields!Total.Value)"));
    }

    [Fact]
    public void TranslateDevExpress_Aggregate_WithDataset()
        => Assert.Equal("$sum(Sales, \"Qty\")", ExpressionTranslator.TranslateDevExpress("Sum([Qty])", "Sales"));

    [Fact]
    public void Translate_Aggregate_WithGroupScopeToken()
    {
        // The reserved $group token (current group's rows) is accepted as the dataset.
        Assert.Equal("$sum($group, \"Total\")",
            ExpressionTranslator.TranslateRdl("=Sum(Fields!Total.Value)", ExpressionTranslator.GroupScopeToken));
        Assert.Equal("$avg($group, \"Qty\")",
            ExpressionTranslator.TranslateDevExpress("Avg([Qty])", ExpressionTranslator.GroupScopeToken));
    }
}
