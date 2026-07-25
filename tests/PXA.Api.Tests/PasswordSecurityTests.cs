using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Api.Tests;

public sealed class PasswordSecurityTests
{
    [Theory]
    [InlineData("Password123!")]
    [InlineData("P@ssw0rd1234")]
    [InlineData("Qwerty123456!")]
    public async Task Known_compromised_passwords_are_rejected(string password)
    {
        await using var provider = CreateProvider();
        var userManager = provider.GetRequiredService<UserManager<PxaIdentityUser>>();
        var result = await userManager.CreateAsync(NewUser(), password);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "PasswordBreached");
    }

    [Fact]
    public async Task A_policy_compliant_non_breached_password_is_accepted()
    {
        await using var provider = CreateProvider();
        var userManager = provider.GetRequiredService<UserManager<PxaIdentityUser>>();
        var result = await userManager.CreateAsync(NewUser(), "Pxa-Unique-Password-42!");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Configured_fingerprints_extend_the_offline_breach_policy()
    {
        const string password = "Pxa-Custom-Breached-42!";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password)));
        var validator = new PxaBreachedPasswordValidator(
            Options.Create(new PxaPasswordSecurityOptions
            {
                BreachedPasswordSha256 = [hash],
            }));

        var result = await validator.ValidateAsync(null!, NewUser(), password);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "PasswordBreached");
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PxaDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddOptions<PxaPasswordSecurityOptions>();
        services.AddIdentityCore<PxaIdentityUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<PxaIdentityRole>()
            .AddEntityFrameworkStores<PxaDbContext>()
            .AddPasswordValidator<PxaBreachedPasswordValidator>();
        return services.BuildServiceProvider();
    }

    private static PxaIdentityUser NewUser() =>
        new()
        {
            UserName = $"{Guid.NewGuid():N}@password.test",
            Email = $"{Guid.NewGuid():N}@password.test",
            DisplayName = "Password Test",
        };
}
