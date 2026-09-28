import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/auth/auth.store';
import { SiteApi } from '../../core/site/site.api';
import { About, ABOUT_LIMITS, AboutInput, CONTACT_MIN } from '../../core/site/site.models';
import { MAX_IMAGE_BYTES, resizeSquare } from '../../core/images/image-resize';
import { NotifyService } from '../../core/ui/notify.service';
import { renderSafeMarkdown } from '../../shared/text/safe-markdown';
import { ContactChannel, ContactIconComponent } from '../../shared/ui/contact-icon/contact-icon.component';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog/confirm-dialog.service';

interface ContactLink {
  channel: ContactChannel;
  label: string;
  href: string;
  text: string;
}

/**
 * "Ο γυμναστής σου" — the trainer's page. Every approved user reads it; an admin edits it in
 * place rather than on a separate screen, so what they are editing is what everyone sees.
 */
@Component({
  selector: 'app-about',
  imports: [FormsModule, ContactIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss',
})
export class AboutComponent {
  private readonly api = inject(SiteApi);
  private readonly notify = inject(NotifyService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly store = inject(AuthStore);

  protected readonly limits = ABOUT_LIMITS;
  protected readonly contactMin = CONTACT_MIN;
  protected readonly isAdmin = computed(() => this.store.user()?.role === 'Admin');

  protected readonly about = signal<About | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly photoBusy = signal(false);
  protected readonly sending = signal(false);
  protected readonly sent = signal(false);

  /** Already escaped and reduced to a known tag set; see renderSafeMarkdown. */
  protected readonly bioHtml = computed(() => renderSafeMarkdown(this.about()?.aboutMarkdown));

  protected readonly links = computed<ContactLink[]>(() => {
    const a = this.about();
    if (!a) return [];

    const links: ContactLink[] = [];
    if (a.contactEmail) links.push({ channel: 'email', label: 'Email', href: `mailto:${a.contactEmail}`, text: a.contactEmail });
    if (a.phone) links.push({ channel: 'phone', label: 'Τηλέφωνο', href: `tel:${a.phone.replace(/\s/g, '')}`, text: a.phone });
    if (a.whatsApp) links.push({ channel: 'whatsapp', label: 'WhatsApp', href: `https://wa.me/${a.whatsApp}`, text: 'WhatsApp' });
    if (a.instagram) links.push({ channel: 'instagram', label: 'Instagram', href: a.instagram, text: 'Instagram' });
    if (a.youTube) links.push({ channel: 'youtube', label: 'YouTube', href: a.youTube, text: 'YouTube' });
    if (a.tikTok) links.push({ channel: 'tiktok', label: 'TikTok', href: a.tikTok, text: 'TikTok' });
    if (a.facebook) links.push({ channel: 'facebook', label: 'Facebook', href: a.facebook, text: 'Facebook' });
    if (a.website) links.push({ channel: 'website', label: 'Ιστοσελίδα', href: a.website, text: 'Ιστοσελίδα' });
    return links;
  });

  /** True when an admin has filled in nothing yet, so the page is not simply blank. */
  protected readonly isEmpty = computed(() => {
    const a = this.about();
    return !a || (!a.trainerName && !a.tagline && !a.aboutMarkdown && !a.photoUrl && this.links().length === 0);
  });

  protected form: AboutInput = blank();
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

  protected startEditing(): void {
    const a = this.about();
    if (!a) return;
    // A copy, so cancelling leaves what is on screen untouched. The photo has its own endpoints
    // and is not part of the form.
    this.form = {
      trainerName: a.trainerName, tagline: a.tagline, aboutMarkdown: a.aboutMarkdown,
      contactEmail: a.contactEmail, phone: a.phone, instagram: a.instagram, youTube: a.youTube,
      tikTok: a.tikTok, facebook: a.facebook, whatsApp: a.whatsApp, website: a.website,
      revision: a.revision,
    };
    this.editing.set(true);
  }

  protected cancelEditing(): void {
    if (!this.saving()) this.editing.set(false);
  }

  protected async save(form: NgForm): Promise<void> {
    if (this.saving()) return;
    if (form.invalid) {
      form.control.markAllAsTouched();
      this.notify.error('Έλεγξε τα στοιχεία που συμπλήρωσες.');
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(this.api.save(this.form));
      await this.load();
      this.editing.set(false);
      this.notify.success('Η σελίδα αποθηκεύτηκε.');
    } catch {
      // The interceptor already said what went wrong.
    } finally {
      this.saving.set(false);
    }
  }

  protected async choosePhoto(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.photoBusy()) return;

    this.photoBusy.set(true);
    try {
      const resized = await resizeSquare(file);
      if (resized === 'type') {
        this.notify.error('Δεκτές εικόνες: JPG, PNG ή WebP.');
        return;
      }
      if (resized === 'size') {
        this.notify.error(`Η εικόνα ξεπερνά τα ${Math.round(MAX_IMAGE_BYTES / 1024 / 1024)} MB.`);
        return;
      }
      if (resized === 'decode') {
        this.notify.error('Η εικόνα δεν διαβάστηκε. Δοκίμασε άλλο αρχείο.');
        return;
      }

      const ticket = await firstValueFrom(this.api.photoTicket(resized.contentType, resized.blob.size));
      const uploaded = await put(ticket.uploadUrl, resized.blob);
      if (!uploaded) {
        this.notify.error('Η φωτογραφία δεν ανέβηκε. Δοκίμασε ξανά.');
        return;
      }

      await firstValueFrom(this.api.confirmPhoto(ticket.objectKey));
      await this.load();
      this.notify.success('Η φωτογραφία ενημερώθηκε.');
    } catch {
      // Reported by the interceptor.
    } finally {
      this.photoBusy.set(false);
    }
  }

  protected async removePhoto(): Promise<void> {
    if (this.photoBusy()) return;
    const ok = await this.confirmDialog.confirm({
      title: 'Αφαίρεση φωτογραφίας',
      message: 'Να αφαιρεθεί η φωτογραφία του γυμναστή;',
      confirmLabel: 'Αφαίρεση',
      destructive: true,
    });
    if (!ok) return;

    this.photoBusy.set(true);
    try {
      await firstValueFrom(this.api.removePhoto());
      await this.load();
    } catch {
      // Reported by the interceptor.
    } finally {
      this.photoBusy.set(false);
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
      await firstValueFrom(this.api.contact({ subject: this.subject.trim(), message: this.message.trim() }));
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

function blank(): AboutInput {
  return {
    trainerName: null, tagline: null, aboutMarkdown: null, contactEmail: null, phone: null,
    instagram: null, youTube: null, tikTok: null, facebook: null, whatsApp: null, website: null,
    revision: '00000000-0000-0000-0000-000000000000',
  };
}

/** Straight PUT to the presigned URL, exactly as the video parts do. */
function put(url: string, body: Blob): Promise<boolean> {
  return new Promise((resolve) => {
    const xhr = new XMLHttpRequest();
    xhr.open('PUT', url, true);
    xhr.onload = () => resolve(xhr.status >= 200 && xhr.status < 300);
    xhr.onerror = () => resolve(false);
    xhr.ontimeout = () => resolve(false);
    xhr.send(body);
  });
}
