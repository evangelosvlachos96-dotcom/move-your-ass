using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.System;
using Mya.Domain.Constants;
using Mya.Domain.Enums;
using Mya.Infrastructure.Identity;

namespace Mya.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates both roles and the first Admin. Idempotent: safe to run on every Development startup.
/// There is no public path to becoming an Admin (docs/03 section 3).
/// </summary>
public sealed class DatabaseSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<AppUser> userManager,
    IOptions<SeedSettings> seedSettings,
    IClock clock,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var role in Roles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureRoleAsync(role);
        }

        await EnsureAdminAsync(seedSettings.Value, cancellationToken);
    }

    private async Task EnsureRoleAsync(string role)
    {
        if (await roleManager.RoleExistsAsync(role))
        {
            return;
        }

        (await roleManager.CreateAsync(new IdentityRole(role))).ThrowIfFailed($"create role '{role}'");
        logger.LogInformation("Seeded role {Role}", role);
    }

    private async Task EnsureAdminAsync(SeedSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.AdminEmail) || string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            throw new InvalidOperationException(
                "Seed:AdminEmail and Seed:AdminPassword must be configured to seed the first Admin. " +
                "Locally: dotnet user-secrets set \"Seed:AdminEmail\" \"...\" --project src/Mya.Api");
        }

        var admin = await userManager.FindByEmailAsync(settings.AdminEmail);

        if (admin is null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = clock.UtcNow;
            admin = new AppUser
            {
                UserName = settings.AdminEmail,
                Email = settings.AdminEmail,
                EmailConfirmed = true,
                FirstName = settings.AdminFirstName,
                LastName = settings.AdminLastName,
                Status = UserStatus.Active,
                CreatedAtUtc = now,
                ApprovedAtUtc = now,
            };

            (await userManager.CreateAsync(admin, settings.AdminPassword)).ThrowIfFailed("create the seed Admin user");
            logger.LogInformation("Seeded Admin user {Email}", settings.AdminEmail);
        }
        else if (string.IsNullOrWhiteSpace(admin.FirstName) || string.IsNullOrWhiteSpace(admin.LastName))
        {
            // Rows created before the FirstName/LastName split carry empty names; fill them once.
            admin.FirstName = string.IsNullOrWhiteSpace(admin.FirstName) ? settings.AdminFirstName : admin.FirstName;
            admin.LastName = string.IsNullOrWhiteSpace(admin.LastName) ? settings.AdminLastName : admin.LastName;
            (await userManager.UpdateAsync(admin)).ThrowIfFailed("fill the seed Admin's names");
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            (await userManager.AddToRoleAsync(admin, Roles.Admin)).ThrowIfFailed("add the seed Admin to the Admin role");
        }
    }
}
