namespace Mya.Infrastructure.Notifications;

public sealed class EmailSettings
{
    public const string SectionName = "Email";
    public string Mode { get; set; } = "Console";
    public string ApiKey { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}
