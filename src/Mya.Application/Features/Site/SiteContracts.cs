using System.Text.RegularExpressions;
using FluentValidation;
using Mya.Application.Common.Results;

namespace Mya.Application.Features.Site;

/// <summary>What the About page shows. Every field is optional; empty ones are hidden by the UI.</summary>
public sealed record AboutDto(
    string? PhotoUrl,
    string? TrainerName,
    string? Tagline,
    string? AboutMarkdown,
    string? ContactEmail,
    string? Phone,
    string? Instagram,
    string? YouTube,
    string? TikTok,
    string? Facebook,
    string? WhatsApp,
    string? Website,
    Guid Revision);

/// <summary>An admin's edit of the About page. <see cref="Revision"/> guards a concurrent save.</summary>
public sealed record AboutInput(
    string? TrainerName,
    string? Tagline,
    string? AboutMarkdown,
    string? ContactEmail,
    string? Phone,
    string? Instagram,
    string? YouTube,
    string? TikTok,
    string? Facebook,
    string? WhatsApp,
    string? Website,
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
        RuleFor(x => x.WhatsApp).MaximumLength(20)
            .Matches("^[0-9]{6,20}$").When(x => !string.IsNullOrWhiteSpace(x.WhatsApp))
            .WithMessage("WhatsApp must be digits only, in international form.");

        foreach (var link in new[] { nameof(AboutInput.Instagram), nameof(AboutInput.YouTube), nameof(AboutInput.TikTok), nameof(AboutInput.Facebook), nameof(AboutInput.Website) })
        {
            RuleFor(x => Value(x, link)).MaximumLength(200)
                .Must(SiteRules.IsSafeUrl).When(x => !string.IsNullOrWhiteSpace(Value(x, link)))
                .WithName(link)
                .WithMessage("Links must be absolute https:// addresses.");
        }
    }

    private static string? Value(AboutInput input, string property) => property switch
    {
        nameof(AboutInput.Instagram) => input.Instagram,
        nameof(AboutInput.YouTube) => input.YouTube,
        nameof(AboutInput.TikTok) => input.TikTok,
        nameof(AboutInput.Facebook) => input.Facebook,
        _ => input.Website,
    };
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

    /// <summary>Photo formats accepted for the trainer picture.</summary>
    public static readonly string[] PhotoContentTypes = ["image/jpeg", "image/png", "image/webp"];

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
