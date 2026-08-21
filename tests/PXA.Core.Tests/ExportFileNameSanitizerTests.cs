using PXA.Core.Primitives;

namespace PXA.Core.Tests;

public sealed class ExportFileNameSanitizerTests
{
    [Theory]
    [InlineData(" Quarterly report ", "Quarterly-report")]
    [InlineData("Invoice: Europe/2026", "Invoice-Europe-2026")]
    [InlineData("   ", "document")]
    [InlineData("...", "document")]
    public void Sanitize_produces_a_safe_non_empty_stem(string input, string expected)
    {
        Assert.Equal(expected, ExportFileNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_limits_the_file_name_length()
    {
        var result = ExportFileNameSanitizer.Sanitize(new string('a', 250));

        Assert.Equal(180, result.Length);
    }
}
