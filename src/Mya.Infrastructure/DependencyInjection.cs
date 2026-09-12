using Microsoft.Extensions.DependencyInjection;

namespace Mya.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the infrastructure layer. Persistence (DbContext, Identity), notifications and
    /// storage are wired here as they are built, starting with step 4 of docs/04 section 7.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
