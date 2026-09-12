namespace Mya.Application.Abstractions.Notifications;

public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Delivers one rendered message. Called only by the outbox dispatcher, never by handlers.</summary>
public interface IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
