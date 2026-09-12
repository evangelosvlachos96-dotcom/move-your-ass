using Mya.Api.Extensions;
using Mya.Api.Middleware;
using Mya.Application;
using Mya.Infrastructure;
using Mya.Infrastructure.Persistence.Seed;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services
        .AddApplication(builder.Configuration)
        .AddInfrastructure(builder.Configuration, builder.Environment)
        .AddApi(builder.Configuration, builder.Environment);

    var app = builder.Build();

    // Roles and the first Admin are seeded on startup in Development only. Production seeding
    // and migrations are deliberate, gated steps (CLAUDE.md "Things that will bite you").
    if (app.Environment.IsDevelopment())
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(CancellationToken.None);

        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.UseCors();

    app.UseMiddleware<AuthRateLimitKeyMiddleware>();
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<MustChangePasswordMiddleware>();

    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is how the EF Core design-time tools stop the host after Build().
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
