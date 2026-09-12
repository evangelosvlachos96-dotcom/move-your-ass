using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Time;
using Mya.Domain.Constants;
using Mya.Domain.Enums;
using Mya.Infrastructure;
using Mya.Infrastructure.Identity;
using Mya.Infrastructure.Persistence;
using Mya.Infrastructure.Persistence.Seed;
using Shouldly;

namespace Mya.Api.IntegrationTests.Persistence;

/// <summary>
/// Runs the real seeder against the real model and Identity stores on an in-memory SQLite
/// database, so it needs no Docker. SQL Server specific behaviour (rowversion, concurrency) is
/// covered by the Testcontainers tests that arrive in step 8, not here.
/// </summary>
public sealed class DatabaseSeederTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@example.test";
    private const string AdminPassword = "SeedAdmin2026x";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider _provider = default!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityStores();
        services.AddSingleton<IClock, UtcClock>();
        services.Configure<SeedSettings>(o =>
        {
            o.AdminEmail = AdminEmail;
            o.AdminPassword = AdminPassword;
        });
        services.AddScoped<DatabaseSeeder>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Seeds_both_roles_and_an_active_admin()
    {
        await SeedAsync();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var roles = await db.Roles.Select(r => r.Name).ToListAsync();
        roles.ShouldBe([Roles.Admin, Roles.Client], ignoreOrder: true);

        var admin = await users.FindByEmailAsync(AdminEmail);
        admin.ShouldNotBeNull();
        admin.Status.ShouldBe(UserStatus.Active);
        admin.EmailConfirmed.ShouldBeTrue();
        admin.ApprovedAtUtc.ShouldNotBeNull();
        (await users.IsInRoleAsync(admin, Roles.Admin)).ShouldBeTrue();
        (await users.CheckPasswordAsync(admin, AdminPassword)).ShouldBeTrue();
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent()
    {
        await SeedAsync();
        await SeedAsync();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Roles.CountAsync()).ShouldBe(2);
        (await db.Users.CountAsync()).ShouldBe(1);
        (await db.UserRoles.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Seeding_repairs_an_admin_that_lost_the_role()
    {
        await SeedAsync();

        using (var scope = _provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var admin = (await users.FindByEmailAsync(AdminEmail))!;
            (await users.RemoveFromRoleAsync(admin, Roles.Admin)).Succeeded.ShouldBeTrue();
        }

        await SeedAsync();

        using var verify = _provider.CreateScope();
        var manager = verify.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var repaired = (await manager.FindByEmailAsync(AdminEmail))!;
        (await manager.IsInRoleAsync(repaired, Roles.Admin)).ShouldBeTrue();
    }

    private async Task SeedAsync()
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(CancellationToken.None);
    }
}
