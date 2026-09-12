using Microsoft.Extensions.Logging;
using Mya.Application.Abstractions.Notifications;

namespace Mya.Infrastructure.Notifications;

/// <summary>Development only: the rendered message goes to Serilog instead of an SMTP server.</summary>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        logger.LogInformation(
            "EMAIL (console sender)\nTo:      {To}\nSubject: {Subject}\n\n{Body}\n",
            message.To,
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }
}
