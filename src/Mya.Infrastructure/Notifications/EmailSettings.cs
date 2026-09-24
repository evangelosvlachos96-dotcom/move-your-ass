namespace Mya.Infrastructure.Notifications;

public sealed class EmailSettings
{
    public const string SectionName = "Email";
    public string Mode { get; set; } = "Console";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Security { get; set; } = "StartTls";
}
