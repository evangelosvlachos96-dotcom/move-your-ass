using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Settings;
using Mya.Application.Common.Time;

namespace Mya.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer: typed settings, the clock, every validator, every handler
    /// and the AutoMapper profiles. Never references the SqlServer provider or ASP.NET.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddOptions<PlatformSettings>()
            .Bind(configuration.GetSection(PlatformSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                s => TimeZoneInfo.TryFindSystemTimeZoneById(s.TimeZone, out _),
                "Platform:TimeZone must be a valid IANA time zone id, e.g. Europe/Athens.")
            .ValidateOnStart();

        services.AddSingleton<IClock, UtcClock>();

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // A handler is a class (ADR-002). Register every *Handler as itself, scoped.
        foreach (var handler in assembly.GetTypes().Where(IsHandler))
        {
            services.AddScoped(handler);
        }

        services.AddAutoMapper(
            cfg =>
            {
                // Community licence key. Never in appsettings; see CLAUDE.md "Licensing notes".
                var licenseKey = Environment.GetEnvironmentVariable("AUTOMAPPER_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    cfg.LicenseKey = licenseKey;
                }
            },
            assembly);

        return services;
    }

    private static bool IsHandler(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && type.Name.EndsWith("Handler", StringComparison.Ordinal);
}
