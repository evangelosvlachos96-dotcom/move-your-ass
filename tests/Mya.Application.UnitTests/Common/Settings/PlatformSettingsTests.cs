using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mya.Application.Common.Settings;
using Shouldly;

namespace Mya.Application.UnitTests.Common.Settings;

public sealed class PlatformSettingsTests
{
    [Fact]
    public void Binds_from_the_Platform_section()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Platform:MaxActiveBookings"] = "3",
            ["Platform:SessionDurationMinutes"] = "45",
            ["Platform:MinBookingNoticeHours"] = "6",
            ["Platform:CancellationWindowHours"] = "48",
            ["Platform:SlotHorizonWeeks"] = "4",
            ["Platform:TimeZone"] = "Europe/Athens",
        });

        var settings = provider.GetRequiredService<IOptions<PlatformSettings>>().Value;

        settings.MaxActiveBookings.ShouldBe(3);
        settings.SessionDurationMinutes.ShouldBe(45);
        settings.MinBookingNoticeHours.ShouldBe(6);
        settings.CancellationWindowHours.ShouldBe(48);
        settings.SlotHorizonWeeks.ShouldBe(4);
        settings.TimeZone.ShouldBe("Europe/Athens");
    }

    [Fact]
    public void Startup_validation_passes_for_the_committed_defaults()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Platform:MaxActiveBookings"] = "5",
            ["Platform:SessionDurationMinutes"] = "60",
            ["Platform:MinBookingNoticeHours"] = "12",
            ["Platform:CancellationWindowHours"] = "24",
            ["Platform:SlotHorizonWeeks"] = "8",
            ["Platform:TimeZone"] = "Europe/Athens",
        });

        Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("Platform:MaxActiveBookings", "0")]
    [InlineData("Platform:SessionDurationMinutes", "5")]
    [InlineData("Platform:SlotHorizonWeeks", "0")]
    [InlineData("Platform:TimeZone", "")]
    [InlineData("Platform:TimeZone", "Mars/Olympus_Mons")]
    public void Startup_validation_rejects_invalid_values(string key, string value)
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { [key] = value });

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new ServiceCollection()
            .AddApplication(configuration)
            .BuildServiceProvider();
    }
}
