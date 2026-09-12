using Microsoft.Extensions.DependencyInjection;

namespace Mya.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer: AutoMapper profiles now; handlers and validators as
    /// features land. Must stay free of EF Core and ASP.NET (see Mya.ArchitectureTests).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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
            typeof(DependencyInjection).Assembly);

        return services;
    }
}
