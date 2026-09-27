using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mya.Application.Abstractions.System;
using Mya.Domain.Constants;
using Mya.Infrastructure;
using Mya.Infrastructure.Identity;
using Mya.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Mya.Api.IntegrationTests.Concurrency;

/// <summary>
/// A real PostgreSQL for the races that only PostgreSQL can answer. SQLite serialises writes, so
/// a "concurrent" test there proves nothing about what two web requests do to Neon: READ
/// COMMITTED lets both transactions read the same row before either writes.
///
/// One container is shared by the whole class; each test uses its own schema-fresh database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Pinned by digest so the tests cannot change behaviour when the tag moves.
    private const string Image = "postgres:17-alpine@sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>
/// One isolated database, with the real EF model and Identity stores over it. Each scope is a
/// separate <see cref="AppDbContext"/>, which is what makes two of them behave like two requests.
/// </summary>
public sealed class IsolatedDatabase : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly List<IServiceScope> _scopes = [];

    private IsolatedDatabase(ServiceProvider provider) => _provider = provider;

    public static async Task<IsolatedDatabase> CreateAsync(PostgresFixture fixture, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var name = "mya_" + Guid.NewGuid().ToString("N");
        var admin = new Npgsql.NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        await using (var connection = new Npgsql.NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        admin.Database = name;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(clock);
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(admin.ConnectionString));
        services.AddIdentityStores();

        var provider = services.BuildServiceProvider();
        var database = new IsolatedDatabase(provider);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in Roles.All)
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        return database;
    }

    /// <summary>A fresh scope, i.e. a fresh DbContext and its own transaction: one "request".</summary>
    public IServiceScope NewScope()
    {
        var scope = _provider.CreateScope();
        _scopes.Add(scope);
        return scope;
    }

    public AppDbContext Context(IServiceScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public IUserServiceFactory Users(IServiceScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new IUserServiceFactory(scope);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _provider.DisposeAsync();
    }
}

/// <summary>Builds a <see cref="UserService"/> bound to one scope's context and clock.</summary>
public readonly struct IUserServiceFactory(IServiceScope scope)
{
    public UserService Build() => new(
        scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<IClock>());
}
