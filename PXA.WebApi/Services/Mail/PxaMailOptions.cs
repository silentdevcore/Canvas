namespace PXA.WebApi.Services.Mail;

public sealed class PxaMailOptions
{
    public string Transport { get; set; } = "Disabled";
    public string AdminBaseUrl { get; set; } = "http://localhost:5177";
    public string SenderName { get; set; } = "Power Dox Automation";
    public string SenderAddress { get; set; } = "no-reply@powerdoxautomation.com";
    public string? ReplyToAddress { get; set; }
    public bool Enabled { get; set; } = true;
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 1025;
    public bool SmtpUseTls { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public int SmtpTimeoutSeconds { get; set; } = 30;

    public bool IsDeliveryEnabled =>
        Enabled && !string.Equals(Transport, "Disabled", StringComparison.OrdinalIgnoreCase);
}
