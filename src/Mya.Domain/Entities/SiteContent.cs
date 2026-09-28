namespace Mya.Domain.Entities;

/// <summary>
/// The "Ο γυμναστής σου" page: one row, edited by admins, read by every approved client.
///
/// A single row rather than a settings key/value table because these fields are edited together
/// on one screen and saved together; a key/value store would turn one optimistic-concurrency
/// check into a dozen. <see cref="Revision"/> is the concurrency token, matching the pattern the
/// video rows already use.
/// </summary>
public sealed class SiteContent
{
    /// <summary>
    /// The fixed identity of the single row. Hardcoded so "get or create" can never race into
    /// two rows: a second insert collides on the primary key rather than succeeding quietly.
    /// </summary>
    public static readonly Guid SingletonId = new("5171ec00-0000-4000-8000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>Provider object key of the trainer photo. Shown only on the About page.</summary>
    public string? PhotoObjectKey { get; set; }

    /// <summary>Bytes the photo occupies, counted against the video storage cap.</summary>
    public long? PhotoSizeBytes { get; set; }

    public string? TrainerName { get; set; }

    public string? Tagline { get; set; }

    /// <summary>
    /// Restricted Markdown: paragraphs, bold, and simple lists. Stored as text and rendered by
    /// the client's own small renderer, which escapes before it formats. Raw HTML is stripped on
    /// the way in and would be escaped on the way out regardless.
    /// </summary>
    public string? AboutMarkdown { get; set; }

    public string? ContactEmail { get; set; }

    public string? Phone { get; set; }

    public string? Instagram { get; set; }

    public string? YouTube { get; set; }

    public string? TikTok { get; set; }

    public string? Facebook { get; set; }

    /// <summary>Digits only, in international form; rendered as a wa.me link.</summary>
    public string? WhatsApp { get; set; }

    public string? Website { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid Revision { get; set; } = Guid.NewGuid();
}
