namespace Mya.Infrastructure.Notifications;

/// <summary>
/// Wakes the <see cref="OutboxDispatcher"/> when a new outbox row has been committed, so the
/// dispatcher does not poll the database while idle. A fixed 15-second poll keeps a serverless
/// database awake around the clock, preventing Neon scale-to-zero and consuming its compute
/// allowance (ADR-017).
/// </summary>
public sealed class OutboxSignal : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    /// <summary>Idempotent: several notifications before the dispatcher wakes collapse into one.</summary>
    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled; the dispatcher will drain everything on its next pass.
        }
    }

    /// <summary>Waits for a notification or the timeout. <see cref="Timeout.InfiniteTimeSpan"/> waits indefinitely.</summary>
    /// <returns><c>true</c> when woken by <see cref="Notify"/>, <c>false</c> on timeout.</returns>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _semaphore.Dispose();
}
