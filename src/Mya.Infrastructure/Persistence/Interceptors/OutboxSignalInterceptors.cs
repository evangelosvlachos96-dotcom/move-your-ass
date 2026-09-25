using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mya.Domain.Entities;
using Mya.Infrastructure.Notifications;

namespace Mya.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Per-context bookkeeping shared by the two interceptors below. Weak keys, so a disposed
/// context never leaks an entry.
/// </summary>
internal static class OutboxCommitTracker
{
    private static readonly ConditionalWeakTable<DbContext, State> States = new();

    public static State For(DbContext context) => States.GetValue(context, static _ => new State());

    internal sealed class State
    {
        /// <summary>The save in progress contains at least one new outbox row.</summary>
        public bool SavingOutbox { get; set; }

        /// <summary>Outbox rows were saved inside a transaction that has not committed yet.</summary>
        public bool AwaitingCommit { get; set; }
    }
}

/// <summary>
/// Signals the dispatcher once new outbox rows are durable. Without an explicit transaction that
/// is right after SaveChanges; inside one it is deferred to <see cref="OutboxTransactionInterceptor"/>,
/// because the dispatcher's claim query skips uncommitted rows (READPAST) and would miss them.
/// </summary>
public sealed class OutboxSaveChangesInterceptor(OutboxSignal signal) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        MarkSaving(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        MarkSaving(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        OnSaved(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        OnSaved(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ClearSaving(eventData.Context);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ClearSaving(eventData.Context);
        return Task.CompletedTask;
    }

    private static void MarkSaving(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        OutboxCommitTracker.For(context).SavingOutbox = context.ChangeTracker
            .Entries<OutboxMessage>()
            .Any(e => e.State == EntityState.Added);
    }

    private static void ClearSaving(DbContext? context)
    {
        if (context is not null)
        {
            OutboxCommitTracker.For(context).SavingOutbox = false;
        }
    }

    private void OnSaved(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var state = OutboxCommitTracker.For(context);
        if (!state.SavingOutbox)
        {
            return;
        }

        state.SavingOutbox = false;
        if (context.Database.CurrentTransaction is null)
        {
            signal.Notify();
        }
        else
        {
            state.AwaitingCommit = true;
        }
    }
}

/// <summary>Completes <see cref="OutboxSaveChangesInterceptor"/> for saves made inside an explicit transaction.</summary>
public sealed class OutboxTransactionInterceptor(OutboxSignal signal) : DbTransactionInterceptor
{
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        OnCommitted(eventData.Context);
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        OnCommitted(eventData.Context);
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Clear(eventData.Context);
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Clear(eventData.Context);
        return Task.CompletedTask;
    }

    private void OnCommitted(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var state = OutboxCommitTracker.For(context);
        if (state.AwaitingCommit)
        {
            state.AwaitingCommit = false;
            signal.Notify();
        }
    }

    private static void Clear(DbContext? context)
    {
        if (context is not null)
        {
            OutboxCommitTracker.For(context).AwaitingCommit = false;
        }
    }
}
