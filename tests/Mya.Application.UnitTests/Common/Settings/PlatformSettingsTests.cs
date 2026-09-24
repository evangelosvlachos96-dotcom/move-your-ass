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
            ["Platform:TimeZone"] = "Europe/Athens",
        });

        var settings = provider.GetRequiredService<IOptions<PlatformSettings>>().Value;

        settings.TimeZone.ShouldBe("Europe/Athens");
    }

    [Fact]
    public void Startup_validation_passes_for_the_committed_defaults()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Platform:TimeZone"] = "Europe/Athens",
        });

        Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
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
