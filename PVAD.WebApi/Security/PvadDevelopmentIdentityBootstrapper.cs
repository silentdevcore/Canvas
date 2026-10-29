using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.Infrastructure.Persistence.Configuration;

namespace PVAD.WebApi.Security;

public static class PvadDevelopmentIdentityBootstrapper
{
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var email = PvadEnvironment.Read("PVAD_BOOTSTRAP_ADMIN_EMAIL");
        var password = PvadEnvironment.Read("PVAD_BOOTSTRAP_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Both PVAD_BOOTSTRAP_ADMIN_EMAIL and PVAD_BOOTSTRAP_ADMIN_PASSWORD are required for development bootstrap.");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<PvadDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<PvadIdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<PvadIdentityUser>>();

        if ((await dbContext.Database.GetPendingMigrationsAsync()).Any())
        {
            throw new InvalidOperationException(
                "Apply PVAD database migrations before bootstrapping a development administrator.");
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
            user = new PvadIdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Local PVADE Administrator",
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

        if (!await userManager.IsInRoleAsync(user, PvadRoles.SystemAdministrator))
        {
            var roleAssignment = await userManager.AddToRoleAsync(user, PvadRoles.SystemAdministrator);
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

    private static async Task EnsureRolesAsync(RoleManager<PvadIdentityRole> roleManager)
    {
        foreach (var definition in PvadRoles.Permissions)
        {
            var role = await roleManager.FindByNameAsync(definition.Key);
            if (role is null)
            {
                role = new PvadIdentityRole
                {
                    Name = definition.Key,
                    Description = $"Built-in PVAD {definition.Key} role.",
                    IsSystemRole = true,
                };
                ThrowIfFailed(await roleManager.CreateAsync(role), $"create role '{definition.Key}'");
            }

            var existingPermissions = (await roleManager.GetClaimsAsync(role))
                .Where(claim => claim.Type == PvadClaimTypes.Permission)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var permission in definition.Value.Where(value => !existingPermissions.Contains(value)))
            {
                ThrowIfFailed(
                    await roleManager.AddClaimAsync(role, new Claim(PvadClaimTypes.Permission, permission)),
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
