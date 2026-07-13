using PXA.Migration.Abstractions;

namespace PXA.Migration.Report.Designer.Rdl.Tests;

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
    public void TranslateRdl_ProducesPxaGrammar(string input, string expected)
        => Assert.Equal(expected, ExpressionTranslator.TranslateRdl(input));

    [Theory]
    [InlineData("[Qty] * [Price]", "Qty * Price")]
    [InlineData("Iif([Ok], 1, 0)", "$iif(Ok, 1, 0)")]
    [InlineData("[Ds.Field]", "Field")]
    [InlineData("[A] == [B]", "A == B")]
    public void TranslateDevExpress_ProducesPxaGrammar(string input, string expected)
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

    [Theory]
    // A computed aggregate argument is translated and emitted single-quoted as a per-row sub-expression.
    [InlineData("=Sum(Fields!Qty.Value * Fields!Price.Value)", "Orders", "$sum(Orders, 'Qty * Price')")]
    [InlineData("=Sum(IIf(Fields!Paid.Value, Fields!Total.Value, 0))", "Orders", "$sum(Orders, '$iif(Paid, Total, 0)')")]
    [InlineData("=Avg(Fields!A.Value + Fields!B.Value)", "Orders", "$avg(Orders, 'A + B')")]
    public void TranslateRdl_ComputedAggregateArgument(string input, string dataSet, string expected)
        => Assert.Equal(expected, ExpressionTranslator.TranslateRdl(input, dataSet));

    [Theory]
    // RunningValue(expr, AggName[, scope]) maps to the matching aggregate over the current scope.
    [InlineData("=RunningValue(Fields!Amount.Value, Sum)", "Sales", "$sum(Sales, \"Amount\")")]
    [InlineData("=RunningValue(Fields!Qty.Value * Fields!Price.Value, Sum, \"g\")", "Sales", "$sum(Sales, 'Qty * Price')")]
    public void TranslateRdl_RunningValue_MapsToAggregate(string input, string dataSet, string expected)
        => Assert.Equal(expected, ExpressionTranslator.TranslateRdl(input, dataSet));
}
