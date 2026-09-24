using System.ComponentModel.DataAnnotations;

namespace Mya.Application.Common.Settings;

/// <summary>
/// Business-rule numbers for the platform. Bound from the "Platform" section of appsettings.json
/// and validated at startup. No handler may hardcode a number that lives here.
/// </summary>
public sealed class PlatformSettings
{
    public const string SectionName = "Platform";

    [Range(1, 168)]
    public int InvitationHours { get; set; } = 24;

    /// <summary>IANA id. The only place the display time zone is named on the server.</summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Athens";
}
