import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type ContactChannel =
  | 'email'
  | 'phone'
  | 'instagram'
  | 'youtube'
  | 'tiktok'
  | 'facebook'
  | 'whatsapp'
  | 'website';

/**
 * A small brand-style glyph for a contact method.
 *
 * Inline SVG rather than emoji: emoji render as somebody else's coloured artwork, differently on
 * every platform, and cannot take the lime accent. These are single paths on `currentColor`, so
 * they inherit the link's colour and stay crisp at any size.
 *
 * Decorative by design — `aria-hidden`, with the accessible name on the surrounding link, so a
 * screen reader hears "Instagram" once rather than twice.
 */
@Component({
  selector: 'app-contact-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      class="contact-icon"
      viewBox="0 0 24 24"
      aria-hidden="true"
      focusable="false"
      [attr.fill]="filled() ? 'currentColor' : 'none'"
      [attr.stroke]="filled() ? 'none' : 'currentColor'"
      stroke-width="1.8"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <path [attr.d]="path()" />
      @if (channel() === 'instagram') {
        <circle cx="12" cy="12" r="3.6" />
        <circle cx="17.2" cy="6.8" r="1" fill="currentColor" stroke="none" />
      }
      @if (channel() === 'email') {
        <path d="M3 7l9 6 9-6" />
      }
    </svg>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
      }
      .contact-icon {
        inline-size: 20px;
        block-size: 20px;
        display: block;
      }
    `,
  ],
})
export class ContactIconComponent {
  readonly channel = input.required<ContactChannel>();

  /** Brand marks read better as solid shapes; the rest as line icons. */
  protected filled = () => FILLED.includes(this.channel());

  protected path = () => PATHS[this.channel()];
}

const FILLED: ContactChannel[] = ['facebook', 'youtube', 'tiktok', 'whatsapp'];

const PATHS: Record<ContactChannel, string> = {
  email: 'M3 5.5h18v13H3z',
  phone:
    'M6.6 3.5h3l1.5 3.8-2 1.4a12 12 0 0 0 5.2 5.2l1.4-2 3.8 1.5v3a1.6 1.6 0 0 1-1.7 1.6A15.6 15.6 0 0 1 5 5.2 1.6 1.6 0 0 1 6.6 3.5z',
  instagram: 'M7.6 3.5h8.8a4.1 4.1 0 0 1 4.1 4.1v8.8a4.1 4.1 0 0 1-4.1 4.1H7.6a4.1 4.1 0 0 1-4.1-4.1V7.6a4.1 4.1 0 0 1 4.1-4.1z',
  youtube:
    'M21.6 7.4a2.5 2.5 0 0 0-1.8-1.8C18.2 5.2 12 5.2 12 5.2s-6.2 0-7.8.4A2.5 2.5 0 0 0 2.4 7.4 26 26 0 0 0 2 12a26 26 0 0 0 .4 4.6 2.5 2.5 0 0 0 1.8 1.8c1.6.4 7.8.4 7.8.4s6.2 0 7.8-.4a2.5 2.5 0 0 0 1.8-1.8A26 26 0 0 0 22 12a26 26 0 0 0-.4-4.6zM10 15.1V8.9l5.2 3.1z',
  tiktok:
    'M16.5 3h-3v12.2a2.4 2.4 0 1 1-2.4-2.4c.2 0 .4 0 .6.1V9.8a5.7 5.7 0 0 0-.6 0 5.5 5.5 0 1 0 5.5 5.5V9.6a6.6 6.6 0 0 0 3.9 1.3V7.8a3.8 3.8 0 0 1-3.8-3.8V3z',
  facebook:
    'M14 8.5V6.8c0-.8.2-1.3 1.4-1.3h1.5V2.6A20 20 0 0 0 14.7 2.5c-2.3 0-3.8 1.4-3.8 4v2H8.3v3h2.6v7.9H14V11.5h2.5l.4-3H14z',
  whatsapp:
    'M12 2.9a9 9 0 0 0-7.7 13.6L3 21.5l5.2-1.3A9 9 0 1 0 12 2.9zm5.2 12.7c-.2.6-1.3 1.2-1.8 1.2-.5 0-.6.3-3.5-1s-3.7-4-3.8-4.2c-.1-.2-.8-1.2-.8-2.3s.5-1.6.7-1.8c.2-.2.4-.3.6-.3h.5c.2 0 .4 0 .6.5l.8 2c.1.2 0 .4-.1.5l-.4.5c-.1.2-.3.3-.1.6a8 8 0 0 0 3.5 3c.3.1.5.1.6 0l.8-1c.2-.2.3-.2.6-.1l2 1c.3.1.4.2.5.3z',
  website: 'M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17zM3.5 12h17M12 3.5c2.2 2.3 3.4 5.3 3.4 8.5S14.2 18.2 12 20.5c-2.2-2.3-3.4-5.3-3.4-8.5S9.8 5.8 12 3.5z',
};
