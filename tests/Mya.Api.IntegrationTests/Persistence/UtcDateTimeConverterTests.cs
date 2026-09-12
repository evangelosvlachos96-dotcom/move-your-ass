using Mya.Infrastructure.Persistence.Conventions;
using Shouldly;

namespace Mya.Api.IntegrationTests.Persistence;

public sealed class UtcDateTimeConverterTests
{
    private readonly UtcDateTimeConverter _converter = new();

    [Fact]
    public void Utc_values_are_written_unchanged()
    {
        var utc = new DateTime(2026, 9, 12, 10, 30, 0, DateTimeKind.Utc);

        var stored = (DateTime)_converter.ConvertToProvider(utc)!;

        stored.ShouldBe(utc);
        stored.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Local_values_are_converted_to_the_same_instant_in_Utc()
    {
        var local = new DateTime(2026, 9, 12, 10, 30, 0, DateTimeKind.Local);

        var stored = (DateTime)_converter.ConvertToProvider(local)!;

        stored.Kind.ShouldBe(DateTimeKind.Utc);
        stored.ShouldBe(local.ToUniversalTime());
    }

    [Fact]
    public void Unspecified_values_are_assumed_to_already_be_Utc()
    {
        var unspecified = new DateTime(2026, 9, 12, 10, 30, 0, DateTimeKind.Unspecified);

        var stored = (DateTime)_converter.ConvertToProvider(unspecified)!;

        stored.Kind.ShouldBe(DateTimeKind.Utc);
        stored.Ticks.ShouldBe(unspecified.Ticks);
    }

    [Fact]
    public void Values_read_back_are_always_Utc()
    {
        var fromDatabase = new DateTime(2026, 9, 12, 10, 30, 0, DateTimeKind.Unspecified);

        var materialised = (DateTime)_converter.ConvertFromProvider(fromDatabase)!;

        materialised.Kind.ShouldBe(DateTimeKind.Utc);
        materialised.Ticks.ShouldBe(fromDatabase.Ticks);
    }

    [Fact]
    public void Round_trip_preserves_the_instant_and_yields_Utc()
    {
        var original = new DateTime(2026, 9, 12, 10, 30, 0, DateTimeKind.Local);

        var stored = (DateTime)_converter.ConvertToProvider(original)!;
        var roundTripped = (DateTime)_converter.ConvertFromProvider(stored)!;

        roundTripped.Kind.ShouldBe(DateTimeKind.Utc);
        roundTripped.ShouldBe(original.ToUniversalTime());
    }
}
