using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Mail;
using Npgsql;
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

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ConnectionStringName} is not configured. " +
                    "Locally: dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\" --project src/Mya.Api");
            }

            // No EnableRetryOnFailure: it rejects the user-initiated transactions the handlers
            // open, and a retried command could run twice. Nothing replaces the old SQL Server
            // connection retry either — Neon resumes from scale-to-zero in a few hundred
            // milliseconds, well inside Npgsql's default 15-second connect timeout (ADR-017).
            // Close idle pooled sockets before Neon's five-minute suspend window.
            var database = new NpgsqlConnectionStringBuilder(connectionString);
            if (!database.ShouldSerialize("Connection Idle Lifetime"))
            {
                database.ConnectionIdleLifetime = 240;
            }

            options.UseNpgsql(database.ConnectionString)
                .AddInterceptors(
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
            .Validate(s => s.Mode is "Console" or "Resend", "Email:Mode must be Console or Resend.")
            .Validate(s => environment.IsDevelopment() || s.Mode == "Resend", "Console email is only allowed in Development.")
            .Validate(s => s.Mode != "Resend" ||
                (!string.IsNullOrWhiteSpace(s.ApiKey) && !s.ApiKey.Any(char.IsWhiteSpace) &&
                 MailAddress.TryCreate(s.From, out _)),
                "Resend requires Email:ApiKey and a valid Email:From address.")
            .ValidateOnStart();
        services.AddOptions<PublicAppSettings>()
            .Bind(configuration.GetSection("App"))
            .Validate(s => Uri.TryCreate(s.PublicOrigin, UriKind.Absolute, out var uri)
                && (uri.Scheme == "https" || (environment.IsDevelopment() && uri.Scheme == "http"))
                && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/",
                "App:PublicOrigin must be an absolute HTTPS origin (HTTP allowed in Development), without a path, query or credentials.")
            .ValidateOnStart();
        services.AddSingleton<EmailTemplates>();
        if (configuration["Email:Mode"] == "Resend")
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
                client.Timeout = TimeSpan.FromSeconds(30))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        else
        {
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }

        services.AddOptions<Mya.Infrastructure.Streaming.BunnySettings>()
            .Bind(configuration.GetSection("Video:Bunny"))
            .Validate(s => !s.Enabled || (s.LibraryId > 0 && !string.IsNullOrWhiteSpace(s.ApiKey)
                && !string.IsNullOrWhiteSpace(s.ReadOnlyApiKey) && !string.IsNullOrWhiteSpace(s.TokenKey)
                && Uri.CheckHostName(s.CdnHost) == UriHostNameType.Dns && s.CdnHost.EndsWith(".b-cdn.net", StringComparison.OrdinalIgnoreCase)),
                "Enabled Bunny Stream requires LibraryId, ApiKey, ReadOnlyApiKey, TokenKey and a b-cdn.net CdnHost.")
            .ValidateOnStart();
        services.AddHttpClient<Mya.Application.Abstractions.Media.IVideoStorage, Mya.Infrastructure.Streaming.BunnyVideoStorage>(client =>
            client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

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
