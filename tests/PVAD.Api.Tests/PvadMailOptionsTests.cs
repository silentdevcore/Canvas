using Microsoft.Extensions.Configuration;
using PVAD.WebApi.Services.Mail;

namespace PVAD.Api.Tests;

public sealed class PvadMailOptionsTests
{
    [Fact]
    public void Defaults_use_approved_sender_without_enabling_delivery()
    {
        var options = new PvadMailOptions();

        Assert.Equal("no-reply@pvade.com", options.SenderAddress);
        Assert.Equal("PVADE", options.SenderName);
        Assert.Equal("Disabled", options.Transport);
        Assert.False(options.IsDeliveryEnabled);
    }

    [Fact]
    public void Explicit_configuration_preserves_customer_sender_and_transport_settings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mail:SenderAddress"] = "sender@customer.example.test",
                ["Mail:SenderName"] = "Customer Documents",
                ["Mail:ReplyToAddress"] = "support@customer.example.test",
                ["Mail:Enabled"] = "false",
                ["Mail:Transport"] = "Smtp",
            }).Build();
        var options = new PvadMailOptions();

        configuration.GetSection("Mail").Bind(options);

        Assert.Equal("sender@customer.example.test", options.SenderAddress);
        Assert.Equal("Customer Documents", options.SenderName);
        Assert.Equal("support@customer.example.test", options.ReplyToAddress);
        Assert.Equal("Smtp", options.Transport);
        Assert.False(options.IsDeliveryEnabled);
    }
}
