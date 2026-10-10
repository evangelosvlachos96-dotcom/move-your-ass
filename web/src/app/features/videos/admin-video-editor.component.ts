import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnDestroy,
  inject,
  signal,
} from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NotifyService } from '../../core/ui/notify.service';
import { reportImageProblem, uploadCover } from '../../core/videos/cover-upload';
import {
  UploadHandle,
  VideoUploadService,
  captureFrame,
} from '../../core/videos/video-upload.service';
import { VideosApi } from '../../core/videos/videos.api';
import {
  ACCEPTED_VIDEO_TYPES,
  Audience,
  BodyArea,
  StorageUsage,
  Tag,
  UploadTicket,
  Video,
  VideoInput,
  sizeLabel,
} from '../../core/videos/video.models';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog/confirm-dialog.service';
import { COVER_CROP, ImageCropService } from '../../shared/ui/image-crop-dialog/image-crop.service';
import { ResizedImage } from '../../core/images/image-resize';

/**
 * One workout, on its own page.
 *
 * This used to be a form that unfolded inside the list, above the cards. With four groups of
 * fields, a tag picker and an upload box it was taller than a phone screen, and while it was open
 * the list it belonged to was pushed out of sight — so it was never clear which video was being
 * edited, or that anything was being edited at all. A page has an address, a title, a back
 * button and one pair of actions at the bottom, and none of that has to be explained.
 *
 * Both routes land here: /admin/videos/new to create, /admin/videos/:id/edit to change one.
 */
@Component({
  selector: 'app-admin-video-editor',
  imports: [SkeletonComponent, FormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-video-editor.component.html',
  styleUrl: './admin-video-editor.component.scss',
})
export class AdminVideoEditorComponent implements OnDestroy {
  private readonly api = inject(VideosApi);
  private readonly uploader = inject(VideoUploadService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notify = inject(NotifyService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cropper = inject(ImageCropService);

  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly busy = signal(false);
  protected readonly uploading = signal(false);
  protected readonly paused = signal(false);
  protected readonly progress = signal<number | null>(null);
  protected readonly note = signal('');
  protected readonly tags = signal<Tag[]>([]);
  protected readonly storage = signal<StorageUsage | null>(null);
  protected readonly configured = signal(false);
  protected readonly coverBusy = signal(false);
  /**
   * A cover chosen while creating, before the video exists to attach it to. Held here and sent
   * the moment the upload completes, so "add a cover" is one thing the trainer does once rather
   * than a second errand after the video appears in the list.
   */
  protected readonly pendingCover = signal<{ image: ResizedImage; preview: string } | null>(null);
  /** The video being edited, once loaded. Null for a new one. */
  protected readonly editing = signal<Video | null>(null);
  /**
   * True when an existing draft is being given a new file, rather than being edited. The upload
   * section then appears for a video that already exists.
   */
  protected readonly recovering = signal(false);

  protected readonly size = sizeLabel;

  protected title = '';
  protected description = '';
  protected audience: Audience | '' = '';
  protected bodyArea: BodyArea | '' = '';
  protected equipment: boolean | null = null;
  protected tagIds: string[] = [];
  protected newTag = '';
  protected file: File | null = null;

  private creationKey = crypto.randomUUID();
  private createdId: string | null = null;
  private upload: UploadHandle | null = null;
  /** What the form looked like when it was loaded, to tell a real edit from an untouched visit. */
  private pristine = '';
  private saved = false;
  /** Where Save and Cancel go back to. The library and the player send admins here with their
   * own address, so editing from a client-facing page returns to that page rather than dumping
   * the admin in the management list. */
  private returnUrl = '/admin/videos';

  constructor() {
    void this.load();
  }

  protected get isNew(): boolean {
    return this.editing() === null;
  }

  /** A new video, or an existing draft being re-uploaded, needs a file before it can be saved. */
  protected get needsFile(): boolean {
    return this.isNew || this.recovering();
  }

  protected get heading(): string {
    return this.isNew ? 'Νέα προπόνηση' : 'Επεξεργασία βίντεο';
  }

  private async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    this.recovering.set(this.route.snapshot.queryParamMap.get('upload') === '1');
    this.returnUrl = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
    this.loading.set(true);
    this.failed.set(false);

    try {
      const [tags, summary] = await Promise.all([
        firstValueFrom(this.api.tags(true)),
        firstValueFrom(this.api.summary()),
      ]);
      this.tags.set(tags);
      this.configured.set(summary.providerConfigured);
      this.storage.set(summary.storage);

      if (id) {
        const video = await firstValueFrom(this.api.detail(id, true));
        this.editing.set(video);
        this.title = video.title;
        this.description = video.description ?? '';
        this.audience = video.audience;
        this.bodyArea = video.bodyArea;
        this.equipment = video.requiresEquipment;
        this.tagIds = video.tags.map((t) => t.id);
        this.createdId = this.recovering() ? video.id : null;
      }

      this.pristine = this.snapshot();
    } catch {
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  /** Everything the Save button would send, as one comparable string. */
  private snapshot(): string {
    return JSON.stringify([
      this.title,
      this.description,
      this.audience,
      this.bodyArea,
      this.equipment,
      [...this.tagIds].sort(),
      this.file?.name ?? null,
    ]);
  }

  protected get dirty(): boolean {
    // Nothing is unsaved before the form has been filled in from the server: until `load()`
    // finishes there is no baseline to compare against, and comparing against an empty one made
    // "go back immediately" look like "you have unsaved changes".
    if (this.loading() || this.pristine === '') return false;
    return !this.saved && this.snapshot() !== this.pristine;
  }

  // --- tags ---------------------------------------------------------------------------------

  protected toggleTag(id: string): void {
    this.tagIds = this.tagIds.includes(id)
      ? this.tagIds.filter((x) => x !== id)
      : [...this.tagIds, id];
  }

  protected async addTag(): Promise<void> {
    if (!this.newTag.trim() || this.busy()) return;
    const existing = this.tags().find(
      (t) =>
        t.name.trim().normalize('NFC').toLocaleLowerCase('el-GR') ===
        this.newTag.trim().normalize('NFC').toLocaleLowerCase('el-GR'),
    );
    if (existing) {
      if (!this.tagIds.includes(existing.id)) this.tagIds = [...this.tagIds, existing.id];
      this.note.set('Η ετικέτα υπάρχει ήδη και είναι επιλεγμένη για αυτό το βίντεο.');
      this.newTag = '';
      return;
    }
    this.busy.set(true);
    try {
      const tag = await firstValueFrom(this.api.addTag(this.newTag.trim()));
      this.tags.set(await firstValueFrom(this.api.tags(true)));
      if (!this.tagIds.includes(tag.id)) this.tagIds = [...this.tagIds, tag.id];
      this.newTag = '';
      this.note.set('Η ετικέτα προστέθηκε και επιλέχθηκε.');
    } catch {
      this.note.set('Δεν αποθηκεύτηκε η ετικέτα.');
    } finally {
      this.busy.set(false);
    }
  }

  // --- the video file -----------------------------------------------------------------------

  protected choose(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.file = null;
    if (!file) {
      this.note.set('');
      return;
    }
    // The same two rules the API enforces, said early so the trainer is not left waiting.
    if (!(ACCEPTED_VIDEO_TYPES as readonly string[]).includes(file.type)) {
      this.note.set(
        'Δεκτά αρχεία: MP4 ή MOV. Στο iPhone επίλεξε Ρυθμίσεις → Κάμερα → Μορφές → «Μέγιστη συμβατότητα».',
      );
      return;
    }
    const limit = this.storage()?.maxFileBytes ?? 0;
    if (!file.size || (limit > 0 && file.size > limit)) {
      this.note.set(`Το αρχείο είναι πολύ μεγάλο. Όριο: ${sizeLabel(limit)}.`);
      return;
    }
    this.file = file;
    this.note.set('');
  }

  // --- cover --------------------------------------------------------------------------------

  protected async chooseCover(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.coverBusy()) return;

    // Framed by the trainer, in the shape the card renders at, before anything is sent.
    const cropped = await this.cropper.crop(file, COVER_CROP, {
      title: 'Εξώφυλλο βίντεο',
      hint: 'Σύρε και μεγέθυνε για να διαλέξεις τι θα φαίνεται στην κάρτα.',
    });
    if (cropped === null) return;
    if (typeof cropped === 'string') {
      reportImageProblem(this.notify, cropped);
      return;
    }

    const video = this.editing();
    if (!video) {
      // Nothing to attach it to yet. Show it straight away and send it after the upload.
      this.releasePreview();
      this.pendingCover.set({ image: cropped, preview: URL.createObjectURL(cropped.blob) });
      return;
    }

    this.coverBusy.set(true);
    try {
      if (await uploadCover(this.api, this.notify, video.id, cropped)) {
        this.editing.set(await firstValueFrom(this.api.detail(video.id, true)));
      }
    } catch {
      // Reported by the error interceptor.
    } finally {
      this.coverBusy.set(false);
    }
  }

  /** What the Εξώφυλλο section is showing, so the label can say which of the three it is. */
  protected get coverKind(): 'pending' | 'custom' | 'frame' | 'none' {
    if (this.pendingCover()) return 'pending';
    const video = this.editing();
    if (video?.hasCustomCover) return 'custom';
    return video?.thumbnailUrl ? 'frame' : 'none';
  }

  protected get coverPreview(): string | null {
    return this.pendingCover()?.preview ?? this.editing()?.thumbnailUrl ?? null;
  }

  protected clearPendingCover(): void {
    this.releasePreview();
    this.pendingCover.set(null);
  }

  private releasePreview(): void {
    const current = this.pendingCover();
    if (current) URL.revokeObjectURL(current.preview);
  }

  protected async removeCover(): Promise<void> {
    const video = this.editing();
    if (!video || this.coverBusy()) return;

    const ok = await this.confirmDialog.confirm({
      title: 'Αφαίρεση εικόνας',
      message: `Να αφαιρεθεί η εικόνα εξωφύλλου από το «${video.title}»;`,
      detail: 'Θα χρησιμοποιηθεί ξανά το καρέ που κρατήθηκε κατά το ανέβασμα, αν υπάρχει.',
      confirmLabel: 'Αφαίρεση',
      destructive: true,
    });
    if (!ok) return;

    this.coverBusy.set(true);
    try {
      await firstValueFrom(this.api.removeCover(video.id));
      this.editing.set(await firstValueFrom(this.api.detail(video.id, true)));
    } catch {
      // Reported by the error interceptor.
    } finally {
      this.coverBusy.set(false);
    }
  }

  // --- saving -------------------------------------------------------------------------------

  protected async save(form: NgForm): Promise<void> {
    if (this.busy() || this.uploading() || this.paused()) return;

    if (
      form.invalid ||
      !this.audience ||
      !this.bodyArea ||
      this.equipment === null ||
      (this.needsFile && !this.file)
    ) {
      form.control.markAllAsTouched();
      this.note.set('Συμπλήρωσε τίτλο, κατηγορίες, εξοπλισμό και αρχείο βίντεο.');
      return;
    }

    const editing = this.editing();
    const input: VideoInput = {
      title: this.title,
      description: this.description || null,
      audience: this.audience,
      bodyArea: this.bodyArea,
      requiresEquipment: this.equipment,
      tagIds: this.tagIds,
      revision: editing?.revision,
    };

    this.busy.set(true);
    this.note.set('');
    try {
      if (editing && !this.recovering()) {
        await firstValueFrom(this.api.update(editing.id, input));
        this.saved = true;
        this.notify.success('Οι αλλαγές αποθηκεύτηκαν.');
        await this.router.navigate([this.returnUrl]);
        return;
      }

      const file = this.file!;
      const request = { contentType: file.type, sizeBytes: file.size };
      if (editing && this.recovering()) {
        await firstValueFrom(this.api.update(editing.id, input));
      }

      let ticket: UploadTicket | null | undefined;
      if (this.createdId) {
        ticket = await firstValueFrom(this.api.upload(this.createdId, request));
      } else {
        const result = await firstValueFrom(this.api.create(input, request, this.creationKey));
        this.createdId = result.id;
        ticket = result.upload;
      }

      if (!ticket) {
        this.note.set(
          'Το βίντεο έχει ήδη δημιουργηθεί. Επέστρεψε στη λίστα για την κατάστασή του.',
        );
        return;
      }

      this.startUpload(file, ticket);
    } catch {
      this.note.set('Δεν ολοκληρώθηκε η ενέργεια. Επέστρεψε στη λίστα πριν αλλάξεις τα στοιχεία.');
    } finally {
      this.busy.set(false);
    }
  }

  private startUpload(file: File, ticket: UploadTicket): void {
    this.uploading.set(true);
    this.paused.set(false);
    this.progress.set(0);
    this.upload = this.uploader.start(file, ticket, {
      progress: (value) => this.progress.set(value),
      done: () => void this.finish(file, ticket.thumbnailUploadUrl),
      failed: (message) => {
        this.uploading.set(false);
        this.paused.set(true);
        this.note.set(message);
      },
    });
  }

  /**
   * The server completes and verifies the upload. A poster frame is best effort: a browser that
   * cannot decode the recording simply sends none, and the library shows the placeholder.
   */
  private async finish(file: File, thumbnailUrl: string): Promise<void> {
    const id = this.createdId!;
    this.note.set('Ολοκλήρωση ανεβάσματος…');

    let thumbnailUploaded = false;
    let duration: number | null = null;
    try {
      const captured = await captureFrame(file);
      duration = captured.duration;
      if (captured.frame) {
        thumbnailUploaded = await this.uploader.uploadThumbnail(thumbnailUrl, captured.frame);
      }
    } catch {
      thumbnailUploaded = false;
    }

    try {
      await firstValueFrom(this.api.completeUpload(id, thumbnailUploaded, duration));

      // The cover the trainer picked before the video existed. Best effort: uploadCover reports
      // its own failures, and a workout without a cover is a workout with a captured frame.
      const chosen = this.pendingCover();
      if (chosen) {
        try {
          await uploadCover(this.api, this.notify, id, chosen.image);
        } catch {
          // Reported by the error interceptor; the video itself is safely uploaded.
        }
        this.clearPendingCover();
      }

      this.uploading.set(false);
      this.paused.set(false);
      this.upload = null;
      this.saved = true;
      this.notify.success('Το βίντεο ανέβηκε. Κάνε προεπισκόπηση και μετά δημοσίευσέ το.');
      await this.router.navigate([this.returnUrl]);
    } catch {
      this.uploading.set(false);
      this.paused.set(true);
      this.note.set('Το ανέβασμα δεν επιβεβαιώθηκε. Πάτησε «Συνέχεια» για νέα προσπάθεια.');
    }
  }

  protected pause(): void {
    this.upload?.pause();
    this.upload = null;
    this.uploading.set(false);
    this.paused.set(true);
    this.note.set('Σε παύση. Πάτησε «Συνέχεια» για να συνεχίσει από εκεί που έμεινε.');
  }

  /** Resume asks the API for a fresh ticket, so it also survives an expired part URL. */
  protected async resume(): Promise<void> {
    if (!this.file || !this.createdId || this.busy()) return;
    this.busy.set(true);
    this.note.set('');
    try {
      const ticket = await firstValueFrom(
        this.api.upload(this.createdId, { contentType: this.file.type, sizeBytes: this.file.size }),
      );
      this.paused.set(false);
      this.startUpload(this.file, ticket);
    } catch {
      this.note.set('Δεν ήταν δυνατή η συνέχιση. Επέστρεψε στη λίστα και δοκίμασε ξανά.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async cancelUpload(): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Διακοπή ανεβάσματος',
      message: 'Να σταματήσει το ανέβασμα;',
      detail: 'Το πρόχειρο βίντεο παραμένει στη λίστα και μπορείς να ξαναδοκιμάσεις αργότερα.',
      confirmLabel: 'Διακοπή',
      cancelLabel: 'Συνέχιση ανεβάσματος',
      destructive: true,
    });
    if (!ok) return;

    this.upload?.cancel();
    this.upload = null;
    this.uploading.set(false);
    this.paused.set(false);

    if (this.createdId) {
      // Abandon the multipart upload so its parts stop using the storage allowance.
      try {
        await firstValueFrom(this.api.abortUpload(this.createdId));
      } catch {
        this.note.set('Το ανέβασμα σταμάτησε, αλλά το πρόχειρο χρειάζεται έλεγχο.');
      }
    }

    this.saved = true;
    await this.router.navigate([this.returnUrl]);
  }

  protected async cancel(): Promise<void> {
    await this.router.navigate([this.returnUrl]);
  }

  /**
   * Leaving with work in it asks first — an upload in flight would be lost, and unsaved fields
   * would be too. Both use the app's own dialog, never the browser's.
   */
  async canLeave(): Promise<boolean> {
    if (this.uploading() || this.paused()) {
      return this.confirmDialog.confirm({
        title: 'Ημιτελές ανέβασμα',
        message: 'Υπάρχει ανέβασμα σε εξέλιξη. Αν φύγεις τώρα θα διακοπεί.',
        detail:
          'Μπορείς να το συνεχίσεις αργότερα από το πρόχειρο βίντεο, επιλέγοντας ξανά το αρχείο.',
        confirmLabel: 'Έξοδος',
        cancelLabel: 'Παραμονή',
        destructive: true,
      });
    }

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
    if (this.uploading() || this.paused() || this.dirty) event.preventDefault();
  }

  ngOnDestroy(): void {
    this.upload?.cancel();
    this.releasePreview();
  }
}

/**
 * Only ever returns a path inside this app.
 *
 * The return address arrives in the URL, where anyone can write it. Without this check a link
 * could send an admin to another site after saving, wearing our page's trust on the way out.
 */
function safeReturnUrl(value: string | null): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : '/admin/videos';
}
