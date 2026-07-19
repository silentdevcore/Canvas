using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

/// <summary>
/// Sibling to <see cref="AdminMutationContractTests"/>: every authenticated
/// Account*Controller mutation must declare authorization, CSRF validation,
/// and an audit action, so a future endpoint added in a later phase cannot
/// silently skip any of the three. <see cref="AccountRegistrationController"/>
/// is excluded on purpose - it is anonymous by design (registration/
/// verification happen before a session exists) and already records its own
/// conditional audit events inline (reviewed in Phase 1), which does not fit
/// the one-attribute-per-action shape this contract enforces. The hardcoded
/// count is intentional friction - bump it deliberately whenever a phase adds
/// Account mutation endpoints.
/// </summary>
public sealed partial class AccountMutationContractTests
{
    private static readonly string[] MutationMethods = ["POST", "PUT", "PATCH", "DELETE"];

    [Fact]
    public void Every_account_mutation_declares_authorization_csrf_and_audit_contracts()
    {
        var mutations = typeof(AccountProfileController).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } &&
                           type.Name.StartsWith("Account", StringComparison.Ordinal) &&
                           typeof(ControllerBase).IsAssignableFrom(type) &&
                           !type.GetCustomAttributes<AllowAnonymousAttribute>(true).Any())
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(method => new
                {
                    Controller = type,
                    Method = method,
                    HttpMethods = method.GetCustomAttributes<HttpMethodAttribute>(true)
                        .SelectMany(attribute => attribute.HttpMethods)
                        .ToArray(),
                }))
            .Where(value => value.HttpMethods.Any(method => MutationMethods.Contains(method, StringComparer.Ordinal)))
            .OrderBy(value => value.Controller.Name, StringComparer.Ordinal)
            .ThenBy(value => value.Method.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(4, mutations.Length);
        foreach (var mutation in mutations)
        {
            var displayName = $"{mutation.Controller.Name}.{mutation.Method.Name}";
            var authorized = mutation.Controller.GetCustomAttributes<AuthorizeAttribute>(true).Any() ||
                             mutation.Method.GetCustomAttributes<AuthorizeAttribute>(true).Any();
            Assert.True(authorized, $"{displayName} must require authorization.");
            Assert.True(
                mutation.Method.GetCustomAttributes<PxaValidateAntiforgeryAttribute>(true).Any(),
                $"{displayName} must validate CSRF tokens.");

            var audit = Assert.Single(mutation.Method.GetCustomAttributes<PxaAuditedMutationAttribute>(true));
            Assert.All(audit.Action.Split('|'), action =>
                Assert.Matches(AuditActionPattern(), action));
        }

        Assert.Equal(
            mutations.Length,
            mutations.Select(value => value.Method.GetCustomAttribute<PxaAuditedMutationAttribute>()!.Action)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [GeneratedRegex("^[a-z][a-z0-9_-]*(\\.[a-z0-9_*][a-z0-9_*-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex AuditActionPattern();
}
