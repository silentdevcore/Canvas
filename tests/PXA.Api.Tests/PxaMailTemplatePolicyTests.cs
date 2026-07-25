using PXA.WebApi.Services.Mail;

namespace PXA.Api.Tests;

public sealed class PxaMailTemplatePolicyTests
{
    [Theory]
    [InlineData("identity.invitation")]
    [InlineData("identity.password-reset")]
    [InlineData("identity.registration-verification")]
    [InlineData("subscription.changed")]
    [InlineData("license.changed")]
    [InlineData("security.organization-changed")]
    public void Approved_transactional_templates_are_allowed(string templateKey)
    {
        Assert.True(PxaMailTemplatePolicy.IsTransactional(templateKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("identity.")]
    [InlineData("identity.unsupported")]
    [InlineData("marketing.newsletter")]
    [InlineData("newsletter.release")]
    public void Non_transactional_templates_are_rejected(string templateKey)
    {
        Assert.False(PxaMailTemplatePolicy.IsTransactional(templateKey));
    }
}
