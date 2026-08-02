using System.Reflection;
using System.Text.Json;

namespace PXA.WebApi.Application.Retention;

public sealed class PxaRetentionPolicyCatalog
{
    private const string ResourceName = "PXA.ProductMetadata.data-processing-inventory.json";
    private readonly PxaProcessingInventory inventory;

    public PxaRetentionPolicyCatalog()
    {
        using var stream = typeof(PxaRetentionPolicyCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded data-processing inventory is missing.");
        inventory = JsonSerializer.Deserialize<PxaProcessingInventory>(stream, new JsonSerializerOptions
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
    public IReadOnlyList<PxaRetentionPolicyDefinition> Policies => inventory.Activities
        .Select(value => new PxaRetentionPolicyDefinition(
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

    private sealed record PxaProcessingInventory(
        bool ProductionApproved,
        string ReviewedAt,
        IReadOnlyList<PxaProcessingActivity> Activities);

    private sealed record PxaProcessingActivity(
        string Id,
        string Name,
        PxaProcessingRetention Retention);

    private sealed record PxaProcessingRetention(
        string Status,
        string ApprovalStatus,
        string Rule,
        IReadOnlyList<string>? Configuration);
}

public sealed record PxaRetentionPolicyDefinition(
    string Id,
    string Name,
    string Status,
    string ApprovalStatus,
    string Rule,
    IReadOnlyList<string> Configuration);
