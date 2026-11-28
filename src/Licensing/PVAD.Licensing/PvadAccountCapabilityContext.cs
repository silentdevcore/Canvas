using System.Collections.Frozen;

namespace PVAD.Licensing;

/// <summary>
/// Trusted host policy intersected with the signed native license. Never grants
/// product rights, never replaces signature verification, and cannot widen a parent.
/// </summary>
internal sealed class PvadAccountCapabilityContext : IDisposable
{
    private static readonly AsyncLocal<PvadAccountCapabilityContext?> Slot = new();
    private readonly PvadAccountCapabilityContext? parent;
    private readonly FrozenSet<string> allowed;
    private int closed;

    private PvadAccountCapabilityContext(IEnumerable<string> capabilities)
    {
        parent = Slot.Value;
        allowed = capabilities.Where(capability =>
            PvadAccountLicensePolicy.Capabilities.Any(known => known.Id == capability) && IsAllowed(capability))
            .ToFrozenSet(StringComparer.Ordinal);
        Slot.Value = this;
    }

    internal static PvadAccountCapabilityContext Enter(PvadLicenseAccountKind account, IEnumerable<string>? approvedMigrations = null)
    {
        var approved = (approvedMigrations ?? []).ToHashSet(StringComparer.Ordinal);
        return new(PvadAccountLicensePolicy.Capabilities.Where(capability =>
            PvadAccountLicensePolicy.Evaluate(account, capability.Id) == PvadAccountLicenseDecision.Allowed ||
            PvadAccountLicensePolicy.Evaluate(account, capability.Id) == PvadAccountLicenseDecision.RequiresAdminApproval &&
            approved.Contains(capability.Id)).Select(capability => capability.Id));
    }

    internal static bool IsAllowed(string capability)
    {
        for (var scope = Slot.Value; scope is not null; scope = scope.parent)
            if (Volatile.Read(ref scope.closed) != 0 || !scope.allowed.Contains(capability)) return false;
        return true;
    }

    internal static void Require(string capability)
    {
        if (!IsAllowed(capability))
            throw PvadLicenseJson.Error("PVAD_ACCOUNT_CAPABILITY_DENIED", "The account does not permit this capability.");
    }

    internal static string[]? Capture() => Slot.Value is null ? null :
        PvadAccountLicensePolicy.Capabilities.Where(value => IsAllowed(value.Id)).Select(value => value.Id).ToArray();

    // Only a trusted parent process supplies this restriction snapshot to a worker.
    // Unknown IDs fail closed; the signed license remains independently mandatory.
    internal static PvadAccountCapabilityContext Restore(IEnumerable<string> capabilities) => new(capabilities);

    public void Dispose()
    {
        if (Volatile.Read(ref closed) != 0) return;
        if (!ReferenceEquals(Slot.Value, this)) throw new InvalidOperationException("Account scopes must close in stack order.");
        Interlocked.Exchange(ref closed, 1);
        Slot.Value = parent;
    }
}
