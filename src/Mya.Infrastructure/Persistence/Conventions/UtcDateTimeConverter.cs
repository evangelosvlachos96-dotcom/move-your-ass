using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mya.Infrastructure.Persistence.Conventions;

/// <summary>
/// Guarantees nothing is ever stored as Local and everything read back is Kind = Utc.
/// Applied to every DateTime and DateTime? via ConfigureConventions.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => ToUtc(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
