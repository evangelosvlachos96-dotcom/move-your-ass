using Microsoft.Extensions.Logging;
using Mya.Application.Abstractions.Notifications;

namespace Mya.Infrastructure.Notifications;

/// <summary>Development only: the rendered message goes to Serilog instead of the email provider.</summary>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The plain-text part only: a terminal cannot render the HTML one, and dumping markup
        // would bury the invitation link that makes this sender useful in Development.
        logger.LogInformation(
            "EMAIL (console sender)\nTo:      {To}\nSubject: {Subject}\n\n{Body}\n[HTML part: {HtmlLength} characters]\n",
            message.To,
            message.Subject,
            message.Text,
            message.Html.Length);

        return Task.CompletedTask;
    }
}
