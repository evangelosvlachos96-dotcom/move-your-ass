namespace Mya.Infrastructure.Persistence.Seed;

/// <summary>Bound from the "Seed" section. Email and password come from user-secrets locally, never from the repo.</summary>
public sealed class SeedSettings
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;

    public string AdminFirstName { get; set; } = "Admin";

    public string AdminLastName { get; set; } = "Account";
}
