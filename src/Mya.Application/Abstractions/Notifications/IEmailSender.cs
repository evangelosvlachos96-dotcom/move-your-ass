namespace Mya.Application.Abstractions.Notifications;

/// <summary>
/// One rendered message. <paramref name="Text"/> and <paramref name="Html"/> say the same thing;
/// both are sent, and the client picks. A text alternative is not optional — some clients block
/// HTML by default, and a message with no text part is more likely to be treated as spam.
/// </summary>
public sealed record EmailMessage(string To, string Subject, string Text, string Html)
{
    /// <summary>
    /// Where a reply should go, when that is not the sending address. The contact form sets it to
    /// the client, so the trainer can just hit reply instead of copying an address out of the body.
    /// </summary>
    public string? ReplyTo { get; init; }
}

/// <summary>Delivers one rendered message. Called only by the outbox dispatcher, never by handlers.</summary>
public interface IEmailSender
{
    public Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken);
}
