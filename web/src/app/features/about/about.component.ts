import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/auth/auth.store';
import { SiteApi } from '../../core/site/site.api';
import { About, CONTACT_MIN, NETWORK_INFO, Network, WHATSAPP } from '../../core/site/site.models';
import { renderSafeMarkdown } from '../../shared/text/safe-markdown';
import { BookingButtonComponent } from '../../shared/ui/booking-button/booking-button.component';
import {
  ContactChannel,
  ContactIconComponent,
} from '../../shared/ui/contact-icon/contact-icon.component';

export interface ContactLink {
  channel: ContactChannel;
  label: string;
  href: string;
  text: string;
  /** Profile pages open in a new tab; mailto, tel and WhatsApp hand off to an app instead. */
  external: boolean;
}

/**
 * "Ο γυμναστής σου" — the trainer's page, as everyone reads it.
 *
 * Read-only. Editing is /about/edit, a page of its own: the in-place version had no clear way
 * back out, and mixing "this is what clients see" with "this is what you are changing" made it
 * impossible to tell which of the two was on screen.
 */
@Component({
  selector: 'app-about',
  imports: [FormsModule, ContactIconComponent, MatIconModule, RouterLink, BookingButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss',
})
export class AboutComponent {
  private readonly api = inject(SiteApi);
  private readonly store = inject(AuthStore);

  protected readonly contactMin = CONTACT_MIN;
  protected readonly isAdmin = this.store.isAdmin;

  protected readonly about = signal<About | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly sending = signal(false);
  protected readonly sent = signal(false);

  /** Already escaped and reduced to a known tag set; see renderSafeMarkdown. */
  protected readonly bioHtml = computed(() => renderSafeMarkdown(this.about()?.aboutMarkdown));

  protected readonly links = computed<ContactLink[]>(() => contactLinks(this.about()));

  /** True when nothing has been filled in yet, so the page explains itself instead of being blank. */
  protected readonly isEmpty = computed(() => {
    const a = this.about();
    return (
      !a ||
      (!a.trainerName && !a.tagline && !a.aboutMarkdown && !a.photoUrl && this.links().length === 0)
    );
  });

  /** The circle's fallback: the trainer's initials when there is a name, the brand mark otherwise. */
  protected readonly initials = computed(() => {
    const name = this.about()?.trainerName?.trim();
    if (!name) return null;
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toLocaleUpperCase('el-GR') ?? '')
      .join('');
  });

  protected subject = '';
  protected message = '';

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      this.about.set(await firstValueFrom(this.api.about()));
    } catch {
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  protected async send(form: NgForm): Promise<void> {
    if (this.sending()) return;
    if (form.invalid) {
      form.control.markAllAsTouched();
      return;
    }

    this.sending.set(true);
    try {
      await firstValueFrom(
        this.api.contact({ subject: this.subject.trim(), message: this.message.trim() }),
      );
      this.subject = '';
      this.message = '';
      form.resetForm();
      this.sent.set(true);
    } catch {
      // Reported by the interceptor, including the rate limit and the duplicate.
    } finally {
      this.sending.set(false);
    }
  }
}

/**
 * The contact rows, in the order they are shown. Exported so the editor can preview exactly what
 * the page will look like rather than approximating it.
 */
export function contactLinks(a: About | null): ContactLink[] {
  if (!a) return [];

  const links: ContactLink[] = [];

  // Email and phone are their own fields: every trainer has them, and they are the two the
  // contact form falls back to. The rest are whatever she chose to add, in her order.
  if (a.contactEmail) {
    links.push({
      channel: 'email',
      label: 'Email',
      href: `mailto:${a.contactEmail}`,
      text: a.contactEmail,
      external: false,
    });
  }
  if (a.phone) {
    links.push({
      channel: 'phone',
      label: 'Τηλέφωνο',
      href: `tel:${a.phone.replace(/\s/g, '')}`,
      text: a.phone,
      external: false,
    });
  }

  for (const link of a.socialLinks ?? []) {
    links.push({
      channel: link.network as ContactChannel,
      label: NETWORK_INFO[link.network]?.label ?? link.network,
      href: link.network === WHATSAPP ? `https://wa.me/${link.value}` : link.value,
      text: OPEN_LABELS[link.network] ?? 'Άνοιξε',
      external: true,
    });
  }

  return links;
}

/** What the row's action reads as, per network. */
const OPEN_LABELS: Partial<Record<Network, string>> = {
  instagram: 'Δες το προφίλ',
  youtube: 'Δες το κανάλι',
  tiktok: 'Δες το προφίλ',
  facebook: 'Δες τη σελίδα',
  whatsapp: 'Στείλε μήνυμα',
  website: 'Άνοιξε τη σελίδα',
};
