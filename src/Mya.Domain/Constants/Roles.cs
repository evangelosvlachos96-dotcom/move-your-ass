namespace Mya.Domain.Constants;

/// <summary>Two roles only (docs/03 section 3). Do not add more without a real reason.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Client = "Client";

    public static readonly IReadOnlyList<string> All = [Admin, Client];
}
