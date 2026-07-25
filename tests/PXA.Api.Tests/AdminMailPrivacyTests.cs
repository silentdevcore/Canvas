using PXA.WebApi.Controllers;

namespace PXA.Api.Tests;

public sealed class AdminMailPrivacyTests
{
    [Theory]
    [InlineData("invited@pxa.test", "i***@p***.test")]
    [InlineData("a@example.com", "a***@e***.com")]
    [InlineData("user@localhost", "u***@l***")]
    [InlineData("invalid", "***")]
    [InlineData("@invalid", "***")]
    public void Recipient_email_is_masked(string recipientEmail, string expected)
    {
        Assert.Equal(expected, AdminMailController.MaskRecipientEmail(recipientEmail));
    }
}
