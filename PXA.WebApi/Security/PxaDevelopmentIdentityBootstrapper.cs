using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.WebApi.Security;

public static class PxaDevelopmentIdentityBootstrapper
{
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var email = Environment.GetEnvironmentVariable("PXA_BOOTSTRAP_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("PXA_BOOTSTRAP_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Both PXA_BOOTSTRAP_ADMIN_EMAIL and PXA_BOOTSTRAP_ADMIN_PASSWORD are required for development bootstrap.");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<PxaDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<PxaIdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<PxaIdentityUser>>();

        if ((await dbContext.Database.GetPendingMigrationsAsync()).Any())
        {
            throw new InvalidOperationException(
                "Apply PXA database migrations before bootstrapping a development administrator.");
        }

        await EnsureRolesAsync(roleManager);

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(value => value.Slug == "local-development");
        if (organization is null)
        {
            organization = new Organization
            {
                Name = "Local Development",
                Slug = "local-development",
            };
            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new PxaIdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Local PXA Administrator",
            };
            var creation = await userManager.CreateAsync(user, password);
            ThrowIfFailed(creation, "create the development administrator");
        }
        else if (!await userManager.CheckPasswordAsync(user, password))
        {
            var passwordErrors = new List<IdentityError>();
            foreach (var validator in userManager.PasswordValidators)
            {
                var validation = await validator.ValidateAsync(userManager, user, password);
                if (!validation.Succeeded)
                    passwordErrors.AddRange(validation.Errors);
            }
            if (passwordErrors.Count > 0)
            {
                ThrowIfFailed(IdentityResult.Failed([.. passwordErrors]),
                    "validate the development administrator password");
            }

            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, password);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.UpdatedAt = DateTimeOffset.UtcNow;
            ThrowIfFailed(await userManager.UpdateAsync(user),
                "synchronize the development administrator password");
        }

        if (!await userManager.IsInRoleAsync(user, PxaRoles.SystemAdministrator))
        {
            var roleAssignment = await userManager.AddToRoleAsync(user, PxaRoles.SystemAdministrator);
            ThrowIfFailed(roleAssignment, "assign the System Administrator role");
        }

        var membershipExists = await dbContext.OrganizationMemberships.AnyAsync(value =>
            value.OrganizationId == organization.Id && value.UserId == user.Id);
        if (!membershipExists)
        {
            dbContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organization.Id,
                UserId = user.Id,
            });
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task EnsureRolesAsync(RoleManager<PxaIdentityRole> roleManager)
    {
        foreach (var definition in PxaRoles.Permissions)
        {
            var role = await roleManager.FindByNameAsync(definition.Key);
            if (role is null)
            {
                role = new PxaIdentityRole
                {
                    Name = definition.Key,
                    Description = $"Built-in PXA {definition.Key} role.",
                    IsSystemRole = true,
                };
                ThrowIfFailed(await roleManager.CreateAsync(role), $"create role '{definition.Key}'");
            }

            var existingPermissions = (await roleManager.GetClaimsAsync(role))
                .Where(claim => claim.Type == PxaClaimTypes.Permission)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var permission in definition.Value.Where(value => !existingPermissions.Contains(value)))
            {
                ThrowIfFailed(
                    await roleManager.AddClaimAsync(role, new Claim(PxaClaimTypes.Permission, permission)),
                    $"assign permission '{permission}' to role '{definition.Key}'");
            }
        }
    }

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        throw new InvalidOperationException(
            $"Failed to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
}
