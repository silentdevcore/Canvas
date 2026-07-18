namespace PXA.WebApi.Services.Mail;

public sealed class PxaMailOptions
{
    public string AdminBaseUrl { get; set; } = "http://localhost:5177";
    public string SenderName { get; set; } = "Power Dox Automation";
    public string SenderAddress { get; set; } = "no-reply@powerdoxautomation.com";
    public bool Enabled { get; set; } = true;
}
