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
}
