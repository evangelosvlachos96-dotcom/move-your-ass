using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MimeKit;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Notifications;
using Mya.Application.Abstractions.Persistence;
using Mya.Infrastructure.Identity;
using Mya.Infrastructure.Notifications;
using Mya.Infrastructure.Persistence;
using Mya.Infrastructure.Persistence.Interceptors;
using Mya.Infrastructure.Persistence.Seed;

namespace Mya.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Default";

    /// <summary>
    /// Registers persistence, the Identity stores, the user and token services, the seeder, the
    /// email sender and the outbox dispatcher. No authentication scheme lives here; the host owns it.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        services.AddSingleton<OutboxSignal>();
        services.AddSingleton<OutboxSaveChangesInterceptor>();
        services.AddSingleton<OutboxTransactionInterceptor>();
        services.AddSingleton<SqlConnectionRetryInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ConnectionStringName} is not configured. " +
                    "Locally: dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\" --project src/Mya.Api");
            }

            options.UseSqlServer(connectionString)
                .AddInterceptors(
                    serviceProvider.GetRequiredService<SqlConnectionRetryInterceptor>(),
                    serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<OutboxTransactionInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityStores();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));
        services.AddScoped<DatabaseSeeder>();

        services.AddOptions<EmailSettings>()
            .Bind(configuration.GetSection(EmailSettings.SectionName))
            .Validate(s => s.Mode is "Console" or "Smtp", "Email:Mode must be Console or Smtp.")
            .Validate(s => environment.IsDevelopment() || s.Mode == "Smtp", "Console email is only allowed in Development.")
            .Validate(s => s.Mode != "Smtp" ||
                (!string.IsNullOrWhiteSpace(s.Host) && s.Port is > 0 and <= 65535 &&
                 !string.IsNullOrWhiteSpace(s.User) && !string.IsNullOrWhiteSpace(s.Password) &&
                 MailboxAddress.TryParse(s.From, out _) && s.Security is "StartTls" or "SslOnConnect"),
                "SMTP requires Host, Port, User, Password, From and StartTls or SslOnConnect security.")
            .ValidateOnStart();
        services.AddSingleton<EmailTemplates>();
        if (configuration["Email:Mode"] == "Smtp")
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }

        services.AddHostedService<OutboxDispatcher>();

        return services;
    }

    /// <summary>
    /// Identity user and role stores on top of <see cref="AppDbContext"/>. Public so tests can
    /// compose it over a different provider without duplicating the policy.
    /// </summary>
    public static IServiceCollection AddIdentityStores(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddIdentityCore<AppUser>(IdentityOptionsSetup.Configure)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        return services;
    }
}
