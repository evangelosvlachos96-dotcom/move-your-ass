namespace Mya.Application.Abstractions.Notifications;

/// <summary>
/// One rendered message. <paramref name="Text"/> and <paramref name="Html"/> say the same thing;
/// both are sent, and the client picks. A text alternative is not optional — some clients block
/// HTML by default, and a message with no text part is more likely to be treated as spam.
/// </summary>
public sealed record EmailMessage(string To, string Subject, string Text, string Html);

/// <summary>Delivers one rendered message. Called only by the outbox dispatcher, never by handlers.</summary>
public interface IEmailSender
{
    public Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken);
}
