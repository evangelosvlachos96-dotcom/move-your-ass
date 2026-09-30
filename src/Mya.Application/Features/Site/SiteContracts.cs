using System.Text.RegularExpressions;
using FluentValidation;
using Mya.Application.Common.Results;

namespace Mya.Application.Features.Site;

/// <summary>
/// One social network the trainer chose to show. <see cref="Network"/> is one of
/// <see cref="SiteRules.Networks"/>; <see cref="Value"/> is an https address, or digits for
/// WhatsApp. Order is the order the trainer arranged them in.
/// </summary>
public sealed record SocialLink(string Network, string Value);

/// <summary>What the About page shows. Every field is optional; empty ones are hidden by the UI.</summary>
public sealed record AboutDto(
    string? PhotoUrl,
    string? TrainerName,
    string? Tagline,
    string? AboutMarkdown,
    string? ContactEmail,
    string? Phone,
    string? BookingUrl,
    IReadOnlyList<SocialLink> SocialLinks,
    Guid Revision);

/// <summary>An admin's edit of the About page. <see cref="Revision"/> guards a concurrent save.</summary>
public sealed record AboutInput(
    string? TrainerName,
    string? Tagline,
    string? AboutMarkdown,
    string? ContactEmail,
    string? Phone,
    string? BookingUrl,
    IReadOnlyList<SocialLink>? SocialLinks,
    Guid Revision);

public sealed class AboutInputValidator : AbstractValidator<AboutInput>
{
    public AboutInputValidator()
    {
        RuleFor(x => x.TrainerName).MaximumLength(120);
        RuleFor(x => x.Tagline).MaximumLength(200);
        RuleFor(x => x.AboutMarkdown).MaximumLength(4000);
        RuleFor(x => x.ContactEmail).MaximumLength(256).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.Phone).MaximumLength(40)
            .Matches(SiteRules.PhonePattern).When(x => !string.IsNullOrWhiteSpace(x.Phone));
        // It becomes an href on an orange button clients are invited to press.
        RuleFor(x => x.BookingUrl).MaximumLength(200)
            .Must(SiteRules.IsSafeUrl).When(x => !string.IsNullOrWhiteSpace(x.BookingUrl))
            .WithMessage("The booking link must be an absolute https:// address.");

        RuleFor(x => x.SocialLinks).Must(links => links is null || links.Count <= SiteRules.Networks.Count)
            .WithMessage("There cannot be more links than there are networks.");

        // A network may appear once. Two Instagram rows would render two identical icons and
        // leave no way to say which one is current.
        RuleFor(x => x.SocialLinks)
            .Must(links => links is null
                || links.Select(l => l.Network).Distinct(StringComparer.Ordinal).Count() == links.Count)
            .WithMessage("Each network can be added only once.");

        RuleForEach(x => x.SocialLinks).ChildRules(link =>
        {
            link.RuleFor(l => l.Network).Must(SiteRules.IsKnownNetwork)
                .WithMessage("Unknown network.");
            link.RuleFor(l => l.Value).NotEmpty().MaximumLength(200);
            link.RuleFor(l => l).Must(SiteRules.IsValidLink)
                .WithMessage("WhatsApp must be digits only; every other link must be an absolute https:// address.");
        });
    }
}

/// <summary>A message a client sends the trainer from the About page.</summary>
public sealed record ContactInput(string Subject, string Message);

public sealed class ContactInputValidator : AbstractValidator<ContactInput>
{
    public ContactInputValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MinimumLength(3).MaximumLength(150);
        RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(4000);
    }
}

public static partial class SiteRules
{
    public const string PhonePattern = @"^[0-9+()\s-]{6,40}$";

    public static readonly Error Forbidden = new("FORBIDDEN", "Active account required", ResultStatus.Forbidden);
    public static readonly Error Conflict = new("SITE_CONFLICT", "The page changed; reload before saving again", ResultStatus.Conflict);
    public static readonly Error Invalid = new("SITE_INVALID", "Invalid request", ResultStatus.Invalid);

    /// <summary>No trainer contact address and no admin to fall back to, so nothing can be sent.</summary>
    public static readonly Error NoRecipient = new("CONTACT_NO_RECIPIENT", "There is nobody to deliver this message to", ResultStatus.Conflict);

    /// <summary>The same message again, moments after the first. Almost always a double submit.</summary>
    public static readonly Error Duplicate = new("CONTACT_DUPLICATE", "That message was just sent", ResultStatus.Conflict);

    /// <summary>
    /// The networks the editor offers, in the order its dropdown lists them. The set lives here
    /// rather than in the schema, so adding one is a code change and not a migration.
    /// </summary>
    public static readonly IReadOnlyList<string> Networks =
        ["instagram", "youtube", "tiktok", "facebook", "whatsapp", "website"];

    /// <summary>WhatsApp is a phone number, not a URL; everything else is a link.</summary>
    public const string WhatsAppNetwork = "whatsapp";

    public static bool IsKnownNetwork(string? network) =>
        network is not null && Networks.Contains(network, StringComparer.Ordinal);

    /// <summary>
    /// A link is valid when its value suits its network: digits for WhatsApp, because it becomes
    /// a wa.me path, and an absolute https address for the rest, because it becomes an href.
    /// </summary>
    public static bool IsValidLink(SocialLink? link) =>
        link is not null
        && IsKnownNetwork(link.Network)
        && (link.Network == WhatsAppNetwork
            ? WhatsAppPattern().IsMatch(link.Value ?? string.Empty)
            : IsSafeUrl(link.Value));

    /// <summary>Drops empties, trims, and keeps the first of any repeated network.</summary>
    public static IReadOnlyList<SocialLink> CleanLinks(IReadOnlyList<SocialLink>? links)
    {
        if (links is null) return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cleaned = new List<SocialLink>(links.Count);
        foreach (var link in links)
        {
            var value = Clean(link?.Value);
            if (link is null || value is null || !IsKnownNetwork(link.Network)) continue;
            if (!seen.Add(link.Network)) continue;
            cleaned.Add(new SocialLink(link.Network, value));
        }

        return cleaned;
    }

    /// <summary>Photo formats accepted for the trainer picture.</summary>
    public static readonly string[] PhotoContentTypes = ["image/jpeg", "image/png", "image/webp"];

    [GeneratedRegex("^[0-9]{6,20}$")]
    private static partial Regex WhatsAppPattern();

    public const long MaxPhotoBytes = 5L * 1024 * 1024;

    public static string PhotoKey(string contentType) =>
        $"site/trainer-{Guid.NewGuid():N}{(contentType == "image/png" ? ".png" : contentType == "image/webp" ? ".webp" : ".jpg")}";

    /// <summary>
    /// Absolute https only. http is refused rather than upgraded, and anything that is not a
    /// URL at all — `javascript:`, a bare handle, a relative path — is refused too, because these
    /// values end up in an href.
    /// </summary>
    public static bool IsSafeUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>
    /// Strips anything that could become markup, before it is ever stored.
    ///
    /// The client renders this with its own small formatter that escapes first, so this is the
    /// second of two defences rather than the only one — but storing clean text means a future
    /// renderer, or an export, cannot reintroduce the problem.
    /// </summary>
    public static string? SanitizeMarkdown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var withoutTags = TagPattern().Replace(value, string.Empty);
        var withoutEntities = withoutTags.Replace("&lt;", string.Empty, StringComparison.Ordinal)
            .Replace("&gt;", string.Empty, StringComparison.Ordinal);

        // Normalise line endings so the stored text renders identically wherever it is read.
        var normalised = withoutEntities.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

        return normalised.Length == 0 ? null : normalised;
    }

    /// <summary>Trims to null, so "saved an empty box" and "never filled it in" are the same state.</summary>
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex TagPattern();
}
