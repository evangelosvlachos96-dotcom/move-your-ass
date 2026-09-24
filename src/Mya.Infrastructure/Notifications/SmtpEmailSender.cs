using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Mya.Application.Abstractions.Notifications;

namespace Mya.Infrastructure.Notifications;

/// <summary>SMTP delivery with metadata-only logging: credentials and email bodies are never logged.</summary>
public sealed class SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        using var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(settings.From));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject;
        email.Body = new TextPart("plain") { Text = message.Body };
        using var client = new SmtpClient();
        client.Timeout = 30000;
        await client.ConnectAsync(settings.Host, settings.Port,
            settings.Security == "SslOnConnect" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
            cancellationToken);
        await client.AuthenticateAsync(settings.User, settings.Password, cancellationToken);
        await client.SendAsync(email, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        logger.LogInformation("SMTP accepted email for delivery");
    }
}
