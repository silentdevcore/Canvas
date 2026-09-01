using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PxaRedactionAuditKeyProviderTests
{
    [Fact]
    public void Constructor_ShouldAllowAnUnconfiguredOptionalAuditKey()
    {
        using var provider = Create(new PxaRedactionAuditKeyOptions());

        Assert.Null(provider.Key);
    }

    [Fact]
    public void Constructor_ShouldFailClosedWhenAConfiguredKeyIsMissing()
    {
        var options = new PxaRedactionAuditKeyOptions
        {
            Pkcs12Path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.p12")
        };

        var error = Assert.Throws<InvalidOperationException>(() => Create(options));

        Assert.Equal("The configured redaction audit signing key was not found.", error.Message);
    }

    [Fact]
    public void Constructor_ShouldFailClosedWhenAConfiguredKeyIsInvalid()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not a PKCS#12 file");
            var options = new PxaRedactionAuditKeyOptions { Pkcs12Path = path };

            var error = Assert.Throws<InvalidOperationException>(() => Create(options));

            Assert.Equal("The configured redaction audit signing key could not be loaded.", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PxaRedactionAuditKeyProvider Create(PxaRedactionAuditKeyOptions options) =>
        new(
            Options.Create(options),
            NullLogger<PxaRedactionAuditKeyProvider>.Instance);
}
