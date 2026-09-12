using System.ComponentModel.DataAnnotations;

namespace Mya.Application.Common.Settings;

/// <summary>
/// Business-rule numbers for the platform. Bound from the "Platform" section of appsettings.json
/// and validated at startup. No handler may hardcode a number that lives here.
/// </summary>
public sealed class PlatformSettings
{
    public const string SectionName = "Platform";

    [Range(1, 100)]
    public int MaxActiveBookings { get; set; } = 5;

    [Range(15, 480)]
    public int SessionDurationMinutes { get; set; } = 60;

    [Range(0, 168)]
    public int MinBookingNoticeHours { get; set; } = 12;

    [Range(0, 168)]
    public int CancellationWindowHours { get; set; } = 24;

    [Range(1, 52)]
    public int SlotHorizonWeeks { get; set; } = 8;

    /// <summary>IANA id. The only place the display time zone is named on the server.</summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Athens";
}
