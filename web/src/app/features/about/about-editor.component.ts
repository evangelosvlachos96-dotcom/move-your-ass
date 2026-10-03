import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ImageProblem, MAX_IMAGE_BYTES } from '../../core/images/image-resize';
import { SiteApi } from '../../core/site/site.api';
import { SiteContentService } from '../../core/site/site-content.service';
import {
  ABOUT_LIMITS,
  About,
  AboutInput,
  NETWORKS,
  NETWORK_INFO,
  Network,
  SocialLink,
  WHATSAPP,
} from '../../core/site/site.models';
import { NotifyService } from '../../core/ui/notify.service';
import { renderSafeMarkdown } from '../../shared/text/safe-markdown';
import { ContactIconComponent } from '../../shared/ui/contact-icon/contact-icon.component';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog/confirm-dialog.service';
import {
  ImageCropService,
  PORTRAIT_CROP,
} from '../../shared/ui/image-crop-dialog/image-crop.service';

/**
 * Editing "Ο γυμναστής σου", on its own route.
 *
 * Previously this unfolded inside the page itself, with native inputs and no way back except
 * finding the Cancel button among the fields. It is now an ordinary form page: a back button,
 * one group of fields per card, and one Save and one Cancel at the bottom that stay in view.
 */
@Component({
  selector: 'app-about-editor',
  imports: [SkeletonComponent, FormsModule, ContactIconComponent, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './about-editor.component.html',
  styleUrl: './about-editor.component.scss',
})
export class AboutEditorComponent {
  private readonly api = inject(SiteApi);
  private readonly notify = inject(NotifyService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);
  private readonly cropper = inject(ImageCropService);
  private readonly siteContent = inject(SiteContentService);

  protected readonly limits = ABOUT_LIMITS;
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly saving = signal(false);
  protected readonly photoBusy = signal(false);
  protected readonly about = signal<About | null>(null);

  /** What the biography will look like once saved, rendered by the very same function. */
  protected readonly preview = computed(() => renderSafeMarkdown(this.bio()));

  protected readonly networkInfo = NETWORK_INFO;
  protected readonly whatsApp = WHATSAPP;

  /** The rows the trainer has added, in her order. Mutated in place by the buttons below. */
  protected readonly links = signal<SocialLink[]>([]);

  /** Networks not yet added, so the dropdown cannot offer a second Instagram. */
  protected readonly available = computed<Network[]>(() => {
    const used = new Set(this.links().map((l) => l.network));
    return NETWORKS.filter((n) => !used.has(n));
  });

  /** What the "add" dropdown currently shows. Reset after each addition. */
  protected pendingNetwork: Network | '' = '';

  protected addLink(): void {
    const network = this.pendingNetwork;
    if (!network || !this.available().includes(network)) return;
    this.links.update((current) => [...current, { network, value: '' }]);
    this.pendingNetwork = '';
  }

  protected removeLink(index: number): void {
    this.links.update((current) => current.filter((_, i) => i !== index));
  }

  /** Moves one row up or down; the order is what the page renders. */
  protected moveLink(index: number, direction: -1 | 1): void {
    const target = index + direction;
    this.links.update((current) => {
      if (target < 0 || target >= current.length) return current;
      const next = [...current];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  protected setLinkValue(index: number, value: string): void {
    this.links.update((current) =>
      current.map((link, i) => (i === index ? { ...link, value } : link)),
    );
  }

  protected form: AboutInput = blank();
  /** Signal-backed so the live preview updates as the biography is typed. */
  protected readonly bio = signal<string | null>(null);
  private pristine = '';
  private saved = false;

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const a = await firstValueFrom(this.api.about());
      this.about.set(a);
      this.form = {
        trainerName: a.trainerName,
        tagline: a.tagline,
        aboutMarkdown: a.aboutMarkdown,
        contactEmail: a.contactEmail,
        phone: a.phone,
        bookingUrl: a.bookingUrl,
        socialLinks: a.socialLinks ?? [],
        revision: a.revision,
      };
      this.bio.set(a.aboutMarkdown);
      this.links.set([...(a.socialLinks ?? [])]);
      this.pristine = this.snapshot();
    } catch {
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private snapshot(): string {
    return JSON.stringify({ ...this.form, aboutMarkdown: this.bio(), socialLinks: this.links() });
  }

  protected get dirty(): boolean {
    // Nothing is unsaved before the form has been filled in from the server: until `load()`
    // finishes there is no baseline to compare against, and comparing against an empty one made
    // "go back immediately" look like "you have unsaved changes".
    if (this.loading() || this.pristine === '') return false;
    return !this.saved && this.snapshot() !== this.pristine;
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
      await firstValueFrom(
        this.api.save({
          ...this.form,
          aboutMarkdown: this.bio(),
          // Rows left blank are dropped rather than saved as an empty link.
          socialLinks: this.links().filter((l) => l.value.trim().length > 0),
        }),
      );

      // The shell draws the booking button from the shared copy, so it has to hear about this
      // save; otherwise adding a link leaves the sidebar empty until the next full page load.
      this.siteContent.set(await firstValueFrom(this.api.about()));
      this.saved = true;
      this.notify.success('Η σελίδα αποθηκεύτηκε.');
      await this.router.navigate(['/about']);
    } catch {
      // The interceptor already said what went wrong, including a stale revision.
    } finally {
      this.saving.set(false);
    }
  }

  protected async cancel(): Promise<void> {
    await this.router.navigate(['/about']);
  }

  // --- photo ----------------------------------------------------------------------------------

  protected async choosePhoto(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.photoBusy()) return;

    // A circle is a cruel crop to do blindly: the centre square of a portrait photograph is
    // usually somebody's chin. The trainer frames it herself, in the circle it will appear in.
    const resized = await this.cropper.crop(file, PORTRAIT_CROP, {
      title: 'Η φωτογραφία σου',
      hint: 'Σύρε και μεγέθυνε για να κεντράρεις το πρόσωπό σου στον κύκλο.',
    });
    if (resized === null) return;
    if (typeof resized === 'string') {
      this.reportProblem(resized);
      return;
    }

    this.photoBusy.set(true);
    try {
      const ticket = await firstValueFrom(
        this.api.photoTicket(resized.contentType, resized.blob.size),
      );
      if (!(await put(ticket.uploadUrl, resized.blob))) {
        this.notify.error('Η φωτογραφία δεν ανέβηκε. Δοκίμασε ξανά.');
        return;
      }

      await firstValueFrom(this.api.confirmPhoto(ticket.objectKey));
      await this.reloadPhoto();
      this.notify.success('Η φωτογραφία ενημερώθηκε.');
    } catch {
      // Reported by the interceptor.
    } finally {
      this.photoBusy.set(false);
    }
  }

  private reportProblem(problem: ImageProblem): void {
    this.notify.error(
      problem === 'type'
        ? 'Δεκτές εικόνες: JPG, PNG ή WebP.'
        : problem === 'size'
          ? `Η εικόνα ξεπερνά τα ${Math.round(MAX_IMAGE_BYTES / 1024 / 1024)} MB.`
          : 'Η εικόνα δεν διαβάστηκε. Δοκίμασε άλλο αρχείο.',
    );
  }

  protected async removePhoto(): Promise<void> {
    if (this.photoBusy() || !this.about()?.photoUrl) return;
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
      await this.reloadPhoto();
    } catch {
      // Reported by the interceptor.
    } finally {
      this.photoBusy.set(false);
    }
  }

  /**
   * The photo has its own endpoints and is saved the moment it is chosen, so re-reading the page
   * must not overwrite fields the trainer is in the middle of typing. Only the photo and the
   * revision are taken from the response.
   */
  private async reloadPhoto(): Promise<void> {
    const a = await firstValueFrom(this.api.about());
    this.about.set(a);
    this.form.revision = a.revision;
    this.pristine = this.snapshot();
  }

  // --- leaving --------------------------------------------------------------------------------

  async canLeave(): Promise<boolean> {
    if (!this.dirty) return true;
    return this.confirmDialog.confirm({
      title: 'Μη αποθηκευμένες αλλαγές',
      message: 'Έχεις αλλαγές που δεν αποθηκεύτηκαν.',
      detail: 'Αν φύγεις τώρα θα χαθούν.',
      confirmLabel: 'Έξοδος χωρίς αποθήκευση',
      cancelLabel: 'Παραμονή',
      destructive: true,
    });
  }

  @HostListener('window:beforeunload', ['$event']) protected warn(event: BeforeUnloadEvent): void {
    if (this.dirty) event.preventDefault();
  }
}

function blank(): AboutInput {
  return {
    trainerName: null,
    tagline: null,
    aboutMarkdown: null,
    contactEmail: null,
    phone: null,
    bookingUrl: null,
    socialLinks: [],
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
