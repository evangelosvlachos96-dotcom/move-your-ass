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

    /// <summary>
    /// Where a client goes to book a session: the trainer's own scheduling page, whatever she
    /// uses. Optional, and every booking button in the app is hidden while it is empty — an
    /// orange call to action that leads nowhere is worse than no button.
    /// </summary>
    public string? BookingUrl { get; set; }

    /// <summary>
    /// The social networks the trainer chose to show, in the order she put them, as a JSON array
    /// of <c>{"network":"instagram","value":"https://…"}</c>.
    ///
    /// A list rather than a column per network: the set of networks is the trainer's decision and
    /// it changes, and a fixed column each meant an editor with eight always-present fields, of
    /// which she filled in two. Adding a network is now data, not a migration.
    ///
    /// JSON in one column rather than a child table because it is read and written whole, always
    /// with its parent row, and is never queried by network. The columns below are the ones this
    /// replaced; they are kept, unused, so the migration that fills the list can be rolled back
    /// without losing anything.
    /// </summary>
    public string? SocialLinksJson { get; set; }

    /// <summary>Superseded by <see cref="SocialLinksJson"/>. Never read; kept so nothing is lost.</summary>
    public string? Instagram { get; set; }

    /// <inheritdoc cref="Instagram"/>
    public string? YouTube { get; set; }

    /// <inheritdoc cref="Instagram"/>
    public string? TikTok { get; set; }

    /// <inheritdoc cref="Instagram"/>
    public string? Facebook { get; set; }

    /// <inheritdoc cref="Instagram"/>
    public string? WhatsApp { get; set; }

    /// <inheritdoc cref="Instagram"/>
    public string? Website { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid Revision { get; set; } = Guid.NewGuid();
}
