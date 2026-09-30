using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.Notifications;

namespace Mya.Infrastructure.Notifications;

/// <summary>HTTPS delivery; retries belong to the outbox and reuse its stable message Id.</summary>
public sealed class ResendEmailSender(
    HttpClient client,
    IOptions<EmailSettings> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("A stable outbox message Id is required.", nameof(messageId));
        }

        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.Add("Idempotency-Key", messageId.ToString("D"));
        // Both parts: the client picks. Sending HTML alone loses readers whose client blocks it
        // and reads as spammier to filters.
        request.Content = JsonContent.Create(new
        {
            from = settings.From,
            to = new[] { message.To },
            subject = message.Subject,
            text = message.Text,
            html = message.Html,
            reply_to = message.ReplyTo,
        });

        using var response = await SendRequestAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Provider bodies can echo submitted content. Store only the status in logs/outbox.
            throw new HttpRequestException(
                $"Resend rejected email delivery (HTTP {(int)response.StatusCode}).",
                inner: null, response.StatusCode);
        }

        logger.LogInformation("Resend accepted outbox message {MessageId}", messageId);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeout is a delivery failure, not a request to stop the dispatcher.
            throw new HttpRequestException("Resend delivery timed out.");
        }
    }
}
