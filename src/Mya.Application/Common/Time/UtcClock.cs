using Mya.Application.Abstractions.System;

namespace Mya.Application.Common.Time;

public sealed class UtcClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
