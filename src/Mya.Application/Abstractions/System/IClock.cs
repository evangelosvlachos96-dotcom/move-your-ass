namespace Mya.Application.Abstractions.System;

/// <summary>All times are UTC (CLAUDE.md rule 3). Inject this instead of calling DateTime.UtcNow.</summary>
public interface IClock
{
    public DateTime UtcNow { get; }
}
