namespace PXA.WebApi.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PxaAuditedMutationAttribute(string action) : Attribute
{
    public string Action { get; } = string.IsNullOrWhiteSpace(action)
        ? throw new ArgumentException("An audit action is required.", nameof(action))
        : action;
}
