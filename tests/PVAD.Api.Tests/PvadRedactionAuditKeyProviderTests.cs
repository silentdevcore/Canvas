using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.Api.Tests;

public sealed class PvadRedactionAuditKeyProviderTests
{
    [Fact]
    public void Constructor_ShouldAllowAnUnconfiguredOptionalAuditKey()
    {
        using var provider = Create(new PvadRedactionAuditKeyOptions());

        Assert.Null(provider.Key);
    }

    [Fact]
    public void Constructor_ShouldFailClosedWhenAConfiguredKeyIsMissing()
    {
        var options = new PvadRedactionAuditKeyOptions
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
            var options = new PvadRedactionAuditKeyOptions { Pkcs12Path = path };

            var error = Assert.Throws<InvalidOperationException>(() => Create(options));

            Assert.Equal("The configured redaction audit signing key could not be loaded.", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PvadRedactionAuditKeyProvider Create(PvadRedactionAuditKeyOptions options) =>
        new(
            Options.Create(options),
            NullLogger<PvadRedactionAuditKeyProvider>.Instance);
}
