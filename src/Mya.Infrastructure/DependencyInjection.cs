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
using Mya.Infrastructure.Storage;
using Mya.Application.Abstractions.Media;

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

            // Npgsql 10 defaults GssEncryptionMode to Prefer, so it attempts Kerberos on every
            // connection. Microsoft's .NET runtime images have not shipped libkrb5 since .NET 8,
            // so each attempt fails and logs "Cannot load library libgssapi_krb5.so.2" — and the
            // failure is an exception per connection, not just a line of noise.
            //
            // Disabled here rather than by installing libgssapi-krb5-2 in the image: Neon is
            // reached over TLS with password authentication and there is no Kerberos realm
            // anywhere in this system, so the library would be a dependency we ship, patch and
            // never use. Set in code rather than in the connection string so it cannot be lost
            // when the owner rotates the Neon credential.
            if (!database.ShouldSerialize("GSS Encryption Mode"))
            {
                database.GssEncryptionMode = GssEncryptionMode.Disable;
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

        AddVideoStorage(services, configuration);

        services.AddHostedService<OutboxDispatcher>();

        return services;
    }

    /// <summary>
    /// Video object storage (ADR-019). <c>Video:Provider</c> exists so the seam stays explicit;
    /// S3-compatible is the only implementation, and Backblaze B2, Cloudflare R2 and MinIO differ
    /// by configuration alone. An unknown provider name fails startup rather than silently
    /// disabling uploads.
    /// </summary>
    private static void AddVideoStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Video:Provider"];
        if (!string.IsNullOrWhiteSpace(provider)
            && !string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Video:Provider '{provider}' is not supported. The only value is 'S3' (ADR-019).");
        }

        services.AddOptions<S3VideoSettings>()
            .Bind(configuration.GetSection("Video:S3"))
            .Validate(
                s => !s.Enabled || (
                    Uri.TryCreate(s.ServiceUrl, UriKind.Absolute, out var url)
                    && url.Scheme == Uri.UriSchemeHttps
                    && url.AbsolutePath == "/"
                    && string.IsNullOrEmpty(url.Query)
                    && string.IsNullOrEmpty(url.UserInfo)),
                "Video:S3:ServiceUrl must be an absolute HTTPS endpoint without a path, query or credentials.")
            .Validate(
                s => !s.Enabled || (!string.IsNullOrWhiteSpace(s.Region)
                    && !string.IsNullOrWhiteSpace(s.BucketName)
                    && !string.IsNullOrWhiteSpace(s.AccessKeyId)
                    && !string.IsNullOrWhiteSpace(s.SecretAccessKey)),
                "Enabled S3 video storage requires Region, BucketName, AccessKeyId and SecretAccessKey.")
            .Validate(
                s => !s.Enabled || (s.PartSizeBytes >= S3VideoSettings.MinimumPartSize
                    && s.PartSizeBytes <= S3VideoSettings.MaximumPartSize),
                "Video:S3:PartSizeBytes must be between 5 MiB and 5 GiB, which every S3 implementation requires.")
            .Validate(
                s => !s.Enabled || (s.MaxFileBytes > 0
                    && s.MaxFileBytes <= s.StorageCapBytes
                    && s.MaxFileBytes <= s.PartSizeBytes * S3VideoSettings.MaximumParts),
                "Video:S3:MaxFileBytes must be positive, no larger than StorageCapBytes, and reachable in 10,000 parts.")
            .Validate(
                s => !s.Enabled || (s.PlaybackMinutes is > 0 and <= 720 && s.UploadMinutes is > 0 and <= 10080),
                "Video:S3:PlaybackMinutes must be 1-720 and UploadMinutes 1-10080 (the SigV4 presign maximum).")
            .ValidateOnStart();

        services.AddSingleton<IVideoStorage, S3VideoStorage>();
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
