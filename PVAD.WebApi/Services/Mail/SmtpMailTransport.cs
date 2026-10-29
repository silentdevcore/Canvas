using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;

namespace PVAD.WebApi.Services.Mail;

public sealed class SmtpMailTransport : IPvadMailTransport
{
    private readonly PvadMailOptions options;

    public SmtpMailTransport(IOptions<PvadMailOptions> options)
    {
        this.options = options.Value;
    }

    public async Task<string> SendAsync(RenderedMail message, CancellationToken cancellationToken)
    {
        using var smtp = new SmtpClient(options.SmtpHost, options.SmtpPort)
        {
            EnableSsl = options.SmtpUseTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = checked(options.SmtpTimeoutSeconds * 1000),
            UseDefaultCredentials = string.IsNullOrWhiteSpace(options.SmtpUsername),
        };
        if (!string.IsNullOrWhiteSpace(options.SmtpUsername))
            smtp.Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword);

        using var mail = CreateMessage(message);
        await smtp.SendMailAsync(mail, cancellationToken);
        return $"smtp:{message.OutboxId}";
    }

    internal MailMessage CreateMessage(RenderedMail message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(options.SenderAddress, options.SenderName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(new MailAddress(message.RecipientEmail));
        if (!string.IsNullOrWhiteSpace(options.ReplyToAddress))
            mail.ReplyToList.Add(new MailAddress(options.ReplyToAddress));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.TextBody,
            null,
            MediaTypeNames.Text.Plain));
        mail.Headers.Add("X-PVAD-Outbox-Id", message.OutboxId.ToString());
        mail.Headers.Add("X-PXA-Outbox-Id", message.OutboxId.ToString());
        return mail;
    }
}

public sealed class DisabledMailTransport : IPvadMailTransport
{
    public Task<string> SendAsync(RenderedMail message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Mail delivery is disabled.");
}
