namespace PXA.Observability.WebhookRelay;

public sealed class WebhookRelayOptions
{
    public const string SectionName = "WebhookRelay";

    public Uri? Destination { get; set; }
    public string SecretFile { get; set; } = string.Empty;
    public int MaxPayloadBytes { get; set; } = 256 * 1024;
    public int RequestTimeoutSeconds { get; set; } = 15;
}
