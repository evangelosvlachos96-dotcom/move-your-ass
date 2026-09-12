namespace Mya.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP host layer: controllers now; filters, ProblemDetails, auth and CORS
    /// as their steps land.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddControllers();

        return services;
    }
}
