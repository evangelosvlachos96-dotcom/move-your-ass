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
/// Polls the outbox every 15 seconds (ADR-010). Rows are claimed with a single
/// UPDATE ... OUTPUT that also counts the attempt and takes a lease, so a second instance or a
/// restart mid-send can never deliver the same row twice. Failures back off 1m, 5m, 30m, 2h and
/// then dead-letter (Attempts = 5, never picked again, LastError says why).
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;
    private const int LastErrorMaxLength = 4000;

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
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

            using var timer = new PeriodicTimer(PollInterval);
            do
            {
                await DispatchBatchAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        List<OutboxMessage> claimed;
        try
        {
            await using var claimScope = scopeFactory.CreateAsyncScope();
            claimed = await ClaimAsync(claimScope.ServiceProvider.GetRequiredService<AppDbContext>(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Outbox: claiming pending messages failed; will retry next tick");
            return;
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
                s => s.SetProperty(m => m.ProcessedAtUtc, now).SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
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
