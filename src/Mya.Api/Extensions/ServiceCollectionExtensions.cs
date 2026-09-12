using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        services.AddAuthorization(options =>
            options.AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Admin)));

        services.AddAuthRateLimiting();
        services.AddSpaCors(configuration);

        if (environment.IsDevelopment())
        {
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
        });
    }

    /// <summary>Exact SPA origin with credentials (docs/03 section 7). Never a wildcard.</summary>
    private static void AddSpaCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origin = configuration["Cors:AllowedOrigin"];

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
