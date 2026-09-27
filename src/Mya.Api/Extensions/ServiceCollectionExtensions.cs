using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Mya.Api.Authorization;
using Mya.Api.Filters;
using Mya.Api.Http;
using Mya.Api.Middleware;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Domain.Constants;
using Mya.Infrastructure.Identity;

namespace Mya.Api.Extensions;

public static class ServiceCollectionExtensions
{
    private const int AuthPermitLimit = 5;
    private static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(15);

    /// <summary>Backstop across every IP for one email. Generous: a real person never meets it.</summary>
    private const int AuthGlobalPermitLimit = 50;
    private static readonly TimeSpan AuthGlobalWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan JwtClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers the HTTP host layer: controllers with the validation filter, ProblemDetails,
    /// JWT bearer authentication, authorization policies, rate limiting, CORS and (Development
    /// only) Swagger with a Bearer button.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();

        services
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Model-binding failures (malformed JSON, missing body) must carry a code like every other failure.
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());

                return new BadRequestObjectResult(ApiProblems.Validation(context.HttpContext, errors))
                {
                    ContentTypes = { ApiProblems.ContentType },
                };
            });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddJwtAuthentication();
        // RequireRole reads the token; CurrentAdminRequirement re-reads the database. Both, so a
        // request without the claim is rejected without a query, and a stale claim is caught.
        services.AddScoped<IAuthorizationHandler, CurrentAdminHandler>();
        services.AddAuthorization(options =>
            options.AddPolicy(Policies.AdminOnly, policy => policy
                .RequireRole(Roles.Admin)
                .AddRequirements(new CurrentAdminRequirement())));

        services.AddAuthRateLimiting();


        if (environment.IsDevelopment())
        {
            services.AddSpaCors(configuration);
            services.AddSwaggerWithBearer();
        }

        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwt) =>
            {
                var settings = jwt.Value;

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = JwtClockSkew,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = AuthClaims.Role,
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                        return ApiProblems.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            ErrorCodes.Unauthenticated,
                            "Authentication required");
                    },
                    OnForbidden = context => ApiProblems.WriteAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        ErrorCodes.Forbidden,
                        "Insufficient permissions"),
                };
            });
    }

    private static void AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await ApiProblems.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    ErrorCodes.RateLimited,
                    "Too many attempts, try again later");
            };

            // Two limits, chained. The tight one is keyed per (email, IP) so one attacker cannot
            // spend the victim's budget: locking someone out of their own account by guessing at
            // their email from elsewhere is exactly what account lockout got wrong, and partition
            // by email alone would reintroduce it. The loose one is keyed per email across every
            // IP, as a backstop against an attempt spread over many addresses; it is generous
            // enough that a real person retrying from a new network never meets it.
            options.AddPolicy(RateLimitPolicies.AuthPerEmail, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    AuthRateLimitKeyMiddleware.PartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = AuthPermitLimit,
                        Window = AuthWindow,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            // The backstop runs as the global limiter rather than a second policy, because
            // [EnableRateLimiting] may only be applied once per endpoint. It passes everything
            // through untouched except the credential endpoints.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                AuthRateLimitKeyMiddleware.IsCredentialEndpoint(httpContext)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        AuthRateLimitKeyMiddleware.EmailOnlyPartitionKey(httpContext),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = AuthGlobalPermitLimit,
                            Window = AuthGlobalWindow,
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        })
                    : RateLimitPartition.GetNoLimiter("unlimited"));
        });
    }

    /// <summary>Exact SPA origin with credentials (docs/03 section 7). Never a wildcard.</summary>
    private static void AddSpaCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origin = configuration["App:PublicOrigin"];

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (!string.IsNullOrWhiteSpace(origin))
            {
                policy.WithOrigins(origin.TrimEnd('/')).AllowCredentials();
            }

            policy.AllowAnyHeader().AllowAnyMethod();
        }));
    }

    private static void AddSwaggerWithBearer(this IServiceCollection services)
    {
        const string scheme = "Bearer";

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Mya API", Version = "v1" });

            options.AddSecurityDefinition(scheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken returned by POST /api/auth/login.",
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(scheme, document)] = [],
            });
        });
    }
}
