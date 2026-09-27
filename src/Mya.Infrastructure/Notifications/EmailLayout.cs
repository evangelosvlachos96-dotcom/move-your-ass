using System.Globalization;
using System.Net;
using System.Text;

namespace Mya.Infrastructure.Notifications;

/// <summary>A call to action. Rendered as a button in HTML and as a labelled URL in text.</summary>
public sealed record EmailCta(string Label, string Url);

/// <summary>
/// What an email says, independent of how it looks. Both the HTML and the plain-text bodies are
/// rendered from this one object, so the two can never drift apart — the usual failure mode of
/// hand-maintaining a text alternative beside a HTML one.
/// </summary>
public sealed record EmailContent(string Heading, IReadOnlyList<string> Paragraphs)
{
    /// <summary>Optional button. Also repeated as a bare URL for clients that strip links.</summary>
    public EmailCta? Cta { get; init; }

    /// <summary>Smaller print under the action, for expiry warnings and the like.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Renders <see cref="EmailContent"/> into an email-safe HTML document and a matching plain-text
/// body. Tables and inline CSS on purpose: Outlook still ignores most of everything else, and
/// email clients strip &lt;style&gt; blocks. Colours mirror web/src/styles/_brand.scss.
///
/// Every value that reaches the HTML goes through <see cref="WebUtility.HtmlEncode"/>: names,
/// reasons and email addresses are user-controlled, and an email body is a scripting surface.
/// </summary>
public static class EmailLayout
{
    private const string Background = "#0e0e10";
    private const string Surface = "#17171a";
    private const string Border = "#26262b";
    private const string Text = "#f5f5f0";
    private const string Muted = "#8b8b84";
    private const string Accent = "#c6f24e";
    private const string OnAccent = "#0e0e10";
    private const string Font = "'Segoe UI',system-ui,-apple-system,Roboto,Helvetica,Arial,sans-serif";

    /// <summary>Gmail refuses to render SVG, so the logo is a PNG served from the site itself.</summary>
    private const string LogoPath = "/email-logo.png";

    private const int LogoWidth = 245;

    public static string Html(EmailContent content, string origin)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        var html = new StringBuilder(2048);
        html.Append(CultureInfo.InvariantCulture,
            $"""
            <!DOCTYPE html>
            <html lang="el"><head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{Encode(content.Heading)}</title>
            </head>
            <body style="margin:0;padding:0;background:{Background};">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{Encode(Preview(content))}</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{Background};">
            <tr><td align="center" style="padding:24px 12px;">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;">
            <tr><td align="center" style="padding:8px 0 20px;">
            <img src="{Encode(origin)}{LogoPath}" width="{LogoWidth}" alt="Move Your Ass" style="display:block;border:0;width:{LogoWidth}px;max-width:80%;height:auto;">
            </td></tr>
            <tr><td style="background:{Surface};border:1px solid {Border};border-radius:14px;padding:32px 28px;">
            <h1 style="margin:0 0 18px;font-family:{Font};font-size:22px;line-height:1.3;font-weight:600;color:{Text};">{Encode(content.Heading)}</h1>
            """);

        foreach (var paragraph in content.Paragraphs)
        {
            html.Append(CultureInfo.InvariantCulture,
                $"""<p style="margin:0 0 14px;font-family:{Font};font-size:15px;line-height:1.65;color:{Text};">{Encode(paragraph)}</p>""");
        }

        if (content.Cta is { } cta)
        {
            // A padded anchor rather than a styled <button>: buttons do not survive most clients.
            html.Append(CultureInfo.InvariantCulture,
                $"""
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:24px 0 8px;"><tr>
                <td align="center" style="background:{Accent};border-radius:10px;">
                <a href="{Encode(cta.Url)}" style="display:inline-block;padding:14px 26px;font-family:{Font};font-size:16px;font-weight:600;color:{OnAccent};text-decoration:none;">{Encode(cta.Label)}</a>
                </td></tr></table>
                <p style="margin:12px 0 0;font-family:{Font};font-size:12px;line-height:1.6;color:{Muted};word-break:break-all;">{Encode(cta.Url)}</p>
                """);
        }

        if (content.Note is { Length: > 0 } note)
        {
            html.Append(CultureInfo.InvariantCulture,
                $"""<p style="margin:18px 0 0;font-family:{Font};font-size:13px;line-height:1.6;color:{Muted};">{Encode(note)}</p>""");
        }

        html.Append(CultureInfo.InvariantCulture,
            $"""
            </td></tr>
            <tr><td style="padding:20px 8px 8px;font-family:{Font};font-size:12px;line-height:1.7;color:{Muted};text-align:center;">
            Move Your Ass — προσωπική βιβλιοθήκη προπονήσεων<br>
            <a href="{Encode(origin)}" style="color:{Muted};text-decoration:underline;">{Encode(origin)}</a><br>
            Αυτό το μήνυμα στάλθηκε αυτόματα. Μην απαντήσεις σε αυτή τη διεύθυνση.
            </td></tr>
            </table></td></tr></table></body></html>
            """);

        return html.ToString();
    }

    public static string PlainText(EmailContent content, string origin)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        var text = new StringBuilder(512);
        text.Append(content.Heading).Append("\n\n");

        foreach (var paragraph in content.Paragraphs)
        {
            text.Append(paragraph).Append("\n\n");
        }

        if (content.Cta is { } cta)
        {
            text.Append(cta.Label).Append(":\n").Append(cta.Url).Append("\n\n");
        }

        if (content.Note is { Length: > 0 } note)
        {
            text.Append(note).Append("\n\n");
        }

        text.Append("—\nMove Your Ass — προσωπική βιβλιοθήκη προπονήσεων\n")
            .Append(origin)
            .Append("\nΑυτό το μήνυμα στάλθηκε αυτόματα. Μην απαντήσεις σε αυτή τη διεύθυνση.\n");

        return text.ToString();
    }

    /// <summary>The line an inbox shows beside the subject. Kept short and free of the URL.</summary>
    private static string Preview(EmailContent content) =>
        content.Paragraphs.Count > 0 ? content.Paragraphs[0] : content.Heading;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
