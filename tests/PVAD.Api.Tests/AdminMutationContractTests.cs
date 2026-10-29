using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed partial class AdminMutationContractTests
{
    private static readonly string[] MutationMethods = ["POST", "PUT", "PATCH", "DELETE"];

    [Fact]
    public void Every_admin_mutation_declares_authorization_csrf_and_audit_contracts()
    {
        var mutations = typeof(AdminUsersController).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } &&
                           type.Name.StartsWith("Admin", StringComparison.Ordinal) &&
                           typeof(ControllerBase).IsAssignableFrom(type))
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

        Assert.Equal(50, mutations.Length);
        Assert.Contains(mutations, value => value.Controller == typeof(AdminLicensesController)
            && value.Method.Name == nameof(AdminLicensesController.ReplaceLicense));
        foreach (var mutation in mutations)
        {
            var displayName = $"{mutation.Controller.Name}.{mutation.Method.Name}";
            var authorized = mutation.Controller.GetCustomAttributes<AuthorizeAttribute>(true).Any() ||
                             mutation.Method.GetCustomAttributes<AuthorizeAttribute>(true).Any();
            Assert.True(authorized, $"{displayName} must require authorization.");
            Assert.True(
                mutation.Method.GetCustomAttributes<PvadValidateAntiforgeryAttribute>(true).Any(),
                $"{displayName} must validate CSRF tokens.");

            var auditAttributes = mutation.Method
                .GetCustomAttributes<PvadAuditedMutationAttribute>(true)
                .ToArray();
            Assert.True(
                auditAttributes.Length == 1,
                $"{displayName} must declare exactly one audited mutation action; found {auditAttributes.Length}.");
            var audit = auditAttributes[0];
            Assert.All(audit.Action.Split('|'), action =>
                Assert.Matches(AuditActionPattern(), action));
        }

        Assert.Equal(
            mutations.Length,
            mutations.Select(value => value.Method.GetCustomAttribute<PvadAuditedMutationAttribute>()!.Action)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [GeneratedRegex("^[a-z][a-z0-9_-]*(\\.[a-z0-9_*][a-z0-9_*-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex AuditActionPattern();
}
