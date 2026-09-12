using Mya.Application.Abstractions.Notifications;

namespace Mya.Infrastructure.Notifications;

/// <summary>
/// Registered outside Development until the SMTP sender lands in phase 4. Sending fails loudly
/// and the outbox keeps the row, so nothing is lost and the gap is visible in LastError.
/// </summary>
public sealed class UnconfiguredEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "No email sender is configured for this environment. SMTP delivery arrives in phase 4 (docs/04-roadmap.md).");
}
