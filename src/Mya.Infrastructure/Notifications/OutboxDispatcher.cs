using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mya.Application.Abstractions.Notifications;
using Mya.Application.Abstractions.System;
using Mya.Domain.Entities;
using Mya.Infrastructure.Persistence;

namespace Mya.Infrastructure.Notifications;

/// <summary>
/// Sends outbox rows (ADR-010) without polling an idle database (ADR-016). The dispatcher drains
/// every claimable row, then sleeps until either <see cref="OutboxSignal"/> reports a newly
/// committed row or the earliest scheduled retry is due. With nothing pending it sleeps
/// indefinitely, so a serverless database can auto-pause. A sweep on startup picks up anything
/// left behind while the app was unloaded or restarted.
/// <para>
/// Rows are claimed with a single UPDATE ... OUTPUT that also counts the attempt and takes a lease,
/// so a second instance or a restart cannot claim a row until its lease expires. Delivery is at
/// least once: a crash after SMTP accepts a message but before marking it processed can result in
/// a duplicate email. Failures back off 1m, 5m, 30m, 2h and then dead-letter (Attempts = 5,
/// never picked again, LastError says why).
/// </para>
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    OutboxSignal signal,
    IClock clock,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;
    private const int LastErrorMaxLength = 4000;
    private const int ClaimFailed = -1;

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorRetryDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DueTimeMargin = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var sleep = await DrainAsync(stoppingToken);
                await signal.WaitAsync(sleep, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    /// <summary>Sends every claimable row, then returns how long the dispatcher may sleep.</summary>
    private async Task<TimeSpan> DrainAsync(CancellationToken cancellationToken)
    {
        int claimedCount;
        do
        {
            claimedCount = await DispatchBatchAsync(cancellationToken);
            if (claimedCount == ClaimFailed)
            {
                return ErrorRetryDelay;
            }
        }
        while (claimedCount == BatchSize);

        return await UntilNextRetryAsync(cancellationToken);
    }

    /// <returns>The number of rows claimed, or <see cref="ClaimFailed"/> when the claim query failed.</returns>
    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        List<OutboxMessage> claimed;
        try
        {
            await using var claimScope = scopeFactory.CreateAsyncScope();
            claimed = await ClaimAsync(claimScope.ServiceProvider.GetRequiredService<AppDbContext>(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Outbox: claiming pending messages failed; retrying in {Delay}", ErrorRetryDelay);
            return ClaimFailed;
        }

        foreach (var message in claimed)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var templates = scope.ServiceProvider.GetRequiredService<EmailTemplates>();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            try
            {
                await sender.SendAsync(templates.Render(message), cancellationToken);
                await MarkProcessedAsync(db, message.Id, cancellationToken);
                logger.LogInformation("Outbox: sent {Type} {MessageId}", message.Type, message.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await MarkFailedAsync(db, message, ex, cancellationToken);
            }
        }

        return claimed.Count;
    }

    /// <summary>
    /// Time until the earliest failed row becomes claimable again, or infinite when nothing is
    /// waiting for a retry. Runs only after a drain, never on an idle timer.
    /// </summary>
    private async Task<TimeSpan> UntilNextRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var nextDueUtc = await db.OutboxMessages
                .AsNoTracking()
                .Where(m => m.ProcessedAtUtc == null && m.Attempts < MaxAttempts && m.LockedUntilUtc != null)
                .MinAsync(m => m.LockedUntilUtc, cancellationToken);

            if (nextDueUtc is null)
            {
                return Timeout.InfiniteTimeSpan;
            }

            var delay = nextDueUtc.Value - clock.UtcNow;
            return delay <= TimeSpan.Zero ? TimeSpan.Zero : delay + DueTimeMargin;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Outbox: reading the next retry time failed; retrying in {Delay}", ErrorRetryDelay);
            return ErrorRetryDelay;
        }
    }

    private async Task<List<OutboxMessage>> ClaimAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var leaseUntil = now.Add(ClaimLease);

        // Executed as-is (not composed), so the UPDATE ... OUTPUT runs verbatim on SQL Server.
        return await db.OutboxMessages
            .FromSql($"""
                UPDATE TOP ({BatchSize}) [OutboxMessage] WITH (ROWLOCK, READPAST, UPDLOCK)
                SET [LockedUntilUtc] = {leaseUntil}, [Attempts] = [Attempts] + 1
                OUTPUT inserted.*
                WHERE [ProcessedAtUtc] IS NULL
                  AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] < {now})
                  AND [Attempts] < {MaxAttempts}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task MarkProcessedAsync(AppDbContext db, Guid messageId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.ProcessedAtUtc, now)
                    .SetProperty(m => m.PayloadJson, "{}")
                    .SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
                cancellationToken);
    }

    private async Task MarkFailedAsync(AppDbContext db, OutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var deadLettered = message.Attempts >= MaxAttempts;
        var retryAt = deadLettered
            ? (DateTime?)null
            : clock.UtcNow.Add(Backoff[Math.Min(message.Attempts, Backoff.Length) - 1]);

        var error = exception.ToString();
        if (error.Length > LastErrorMaxLength)
        {
            error = error[..LastErrorMaxLength];
        }

        await db.OutboxMessages
            .Where(m => m.Id == message.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.LastError, error).SetProperty(m => m.LockedUntilUtc, retryAt),
                cancellationToken);

        if (deadLettered)
        {
            logger.LogError(exception, "Outbox: {Type} {MessageId} dead-lettered after {Attempts} attempts", message.Type, message.Id, message.Attempts);
        }
        else
        {
            logger.LogWarning(exception, "Outbox: {Type} {MessageId} failed (attempt {Attempts}); retry at {RetryAt:u}", message.Type, message.Id, message.Attempts, retryAt);
        }
    }
}
