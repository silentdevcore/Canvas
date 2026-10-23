using System.Reflection;
using System.Text.Json;

namespace PVAD.WebApi.Application.Retention;

public sealed class PvadRetentionPolicyCatalog
{
    private const string ResourceName = "PVAD.ProductMetadata.data-processing-inventory.json";
    private readonly PvadProcessingInventory inventory;

    public PvadRetentionPolicyCatalog()
    {
        using var stream = typeof(PvadRetentionPolicyCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded data-processing inventory is missing.");
        inventory = JsonSerializer.Deserialize<PvadProcessingInventory>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("The data-processing inventory is invalid.");

        if (inventory.Activities.Count == 0 || inventory.Activities.Any(value =>
                string.IsNullOrWhiteSpace(value.Id) ||
                string.IsNullOrWhiteSpace(value.Retention.Status) ||
                string.IsNullOrWhiteSpace(value.Retention.ApprovalStatus)))
        {
            throw new InvalidOperationException("Every processing activity requires a retention policy and approval state.");
        }
    }

    public bool ProductionApproved => inventory.ProductionApproved;
    public DateOnly ReviewedAt => DateOnly.Parse(inventory.ReviewedAt, System.Globalization.CultureInfo.InvariantCulture);
    public IReadOnlyList<PvadRetentionPolicyDefinition> Policies => inventory.Activities
        .Select(value => new PvadRetentionPolicyDefinition(
            value.Id,
            value.Name,
            value.Retention.Status,
            value.Retention.ApprovalStatus,
            value.Retention.Rule,
            value.Retention.Configuration ?? []))
        .ToArray();

    public bool ContainsCategory(string category) =>
        inventory.Activities.Any(value => string.Equals(value.Id, category, StringComparison.Ordinal));

    public bool IsProductionReady =>
        ProductionApproved && Policies.All(value => value.ApprovalStatus == "approved");

    private sealed record PvadProcessingInventory(
        bool ProductionApproved,
        string ReviewedAt,
        IReadOnlyList<PvadProcessingActivity> Activities);

    private sealed record PvadProcessingActivity(
        string Id,
        string Name,
        PvadProcessingRetention Retention);

    private sealed record PvadProcessingRetention(
        string Status,
        string ApprovalStatus,
        string Rule,
        IReadOnlyList<string>? Configuration);
}

public sealed record PvadRetentionPolicyDefinition(
    string Id,
    string Name,
    string Status,
    string ApprovalStatus,
    string Rule,
    IReadOnlyList<string> Configuration);
