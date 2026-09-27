import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnDestroy,
  inject,
  signal,
} from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { VideosApi } from '../../core/videos/videos.api';
import {
  UploadHandle,
  VideoUploadService,
  captureFrame,
} from '../../core/videos/video-upload.service';
import {
  ACCEPTED_VIDEO_TYPES,
  Audience,
  BodyArea,
  STATUS_LABELS,
  StorageUsage,
  Tag,
  UploadTicket,
  Video,
  VideoInput,
  VideoPage,
  sizeLabel,
} from '../../core/videos/video.models';
@Component({
  selector: 'app-admin-videos',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-videos.component.html',
  styleUrl: './videos.scss',
})
export class AdminVideosComponent implements OnDestroy {
  private readonly api = inject(VideosApi);
  private readonly uploader = inject(VideoUploadService);
  protected readonly page = signal<VideoPage | null>(null);
  protected readonly tags = signal<Tag[]>([]);
  protected readonly busy = signal(false);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly editor = signal(false);
  protected readonly progress = signal<number | null>(null);
  protected readonly uploading = signal(false);
  protected readonly paused = signal(false);
  protected readonly note = signal('');
  protected readonly configured = signal(false);
  protected readonly storage = signal<StorageUsage | null>(null);
  protected readonly status = STATUS_LABELS;
  protected readonly size = sizeLabel;
  protected search = '';
  protected pageNumber = 1;
  protected editing: Video | null = null;
  protected recovering = false;
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

  /** Percentage of the storage allowance in use, for the bar and its warning band. */
  protected get storagePercent(): number {
    const usage = this.storage();
    return usage && usage.capBytes > 0
      ? Math.min(100, Math.round((usage.usedBytes / usage.capBytes) * 100))
      : 0;
  }

  protected get storageNearlyFull(): boolean {
    return this.storagePercent >= 80;
  }

  constructor() {
    void this.load();
    void this.loadTags();
    void this.loadSummary();
  }

  protected recover(video: Video): void {
    this.open(video);
    this.recovering = true;
    this.createdId = video.id;
  }

  protected async load(page = this.pageNumber): Promise<void> {
    this.pageNumber = page;
    this.loading.set(true);
    this.failed.set(false);
    try {
      this.page.set(
        await firstValueFrom(this.api.list({ page, pageSize: 12, search: this.search }, true)),
      );
    } catch {
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadTags(): Promise<void> {
    try {
      this.tags.set(await firstValueFrom(this.api.tags(true)));
    } catch {
      this.note.set('Δεν ήταν δυνατή η φόρτωση ετικετών.');
    }
  }

  private async loadSummary(): Promise<void> {
    try {
      const summary = await firstValueFrom(this.api.summary());
      this.configured.set(summary.providerConfigured);
      this.storage.set(summary.storage);
    } catch {
      this.note.set('Δεν ήταν δυνατός ο έλεγχος μεταφόρτωσης.');
    }
  }

  protected open(video: Video | null = null): void {
    if (this.uploading() || this.paused()) return;
    this.recovering = false;
    this.editing = video;
    this.title = video?.title ?? '';
    this.description = video?.description ?? '';
    this.audience = video?.audience ?? '';
    this.bodyArea = video?.bodyArea ?? '';
    this.equipment = video?.requiresEquipment ?? null;
    this.tagIds = video?.tags.map((t) => t.id) ?? [];
    this.file = null;
    this.createdId = null;
    this.creationKey = crypto.randomUUID();
    this.progress.set(null);
    this.note.set('');
    this.editor.set(true);
  }

  protected close(): void {
    if (!this.uploading() && !this.paused() && !this.busy()) this.editor.set(false);
  }

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

  protected toggleTag(id: string): void {
    this.tagIds = this.tagIds.includes(id)
      ? this.tagIds.filter((x) => x !== id)
      : [...this.tagIds, id];
  }

  protected async addTag(): Promise<void> {
    if (!this.newTag.trim() || this.busy()) return;
    this.busy.set(true);
    try {
      const t = await firstValueFrom(this.api.addTag(this.newTag.trim()));
      await this.loadTags();
      if (!this.tagIds.includes(t.id)) this.tagIds.push(t.id);
      this.newTag = '';
    } catch {
      this.note.set('Δεν αποθηκεύτηκε η ετικέτα.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async removeTag(tag: Tag): Promise<void> {
    if (tag.usageCount || !confirm('Διαγραφή της ετικέτας «' + tag.name + '»;')) return;
    try {
      await firstValueFrom(this.api.deleteTag(tag.id));
      await this.loadTags();
      this.tagIds = this.tagIds.filter((x) => x !== tag.id);
    } catch {
      this.note.set('Η ετικέτα χρησιμοποιείται ή άλλαξε. Ανανέωσε τη λίστα.');
    }
  }

  protected async save(form: NgForm): Promise<void> {
    if (this.busy() || this.uploading() || this.paused()) return;
    if (
      form.invalid ||
      !this.audience ||
      !this.bodyArea ||
      this.equipment === null ||
      ((!this.editing || this.recovering) && !this.file)
    ) {
      form.control.markAllAsTouched();
      this.note.set('Συμπλήρωσε τίτλο, κατηγορίες, εξοπλισμό και αρχείο βίντεο.');
      return;
    }
    const input: VideoInput = {
      title: this.title,
      description: this.description || null,
      audience: this.audience,
      bodyArea: this.bodyArea,
      requiresEquipment: this.equipment,
      tagIds: this.tagIds,
      revision: this.editing?.revision,
    };
    this.busy.set(true);
    this.note.set('');
    try {
      if (this.editing && !this.recovering) {
        await firstValueFrom(this.api.update(this.editing.id, input));
        this.editor.set(false);
        await this.load();
        return;
      }
      const file = this.file!;
      const request = { contentType: file.type, sizeBytes: file.size };
      if (this.editing && this.recovering) {
        await firstValueFrom(this.api.update(this.editing.id, input));
      }
      let ticket;
      if (this.createdId) ticket = await firstValueFrom(this.api.upload(this.createdId, request));
      else {
        const result = await firstValueFrom(this.api.create(input, request, this.creationKey));
        this.createdId = result.id;
        ticket = result.upload;
      }
      if (!ticket) {
        this.note.set('Το βίντεο έχει ήδη δημιουργηθεί. Ανανέωσε τη λίστα για την κατάστασή του.');
        await this.load();
        return;
      }
      this.startUpload(file, ticket);
    } catch {
      this.note.set('Δεν ολοκληρώθηκε η ενέργεια. Ανανέωσε τη λίστα πριν αλλάξεις τα στοιχεία.');
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
      this.uploading.set(false);
      this.paused.set(false);
      this.upload = null;
      this.note.set('Το βίντεο ανέβηκε. Κάνε προεπισκόπηση και μετά δημοσίευσέ το.');
      this.editor.set(false);
    } catch {
      this.uploading.set(false);
      this.paused.set(true);
      this.note.set('Το ανέβασμα δεν επιβεβαιώθηκε. Πάτησε «Συνέχεια» για νέα προσπάθεια.');
    }
    await this.load();
    await this.loadSummary();
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
      this.note.set('Δεν ήταν δυνατή η συνέχιση. Ανανέωσε τη λίστα και δοκίμασε ξανά.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async cancelUpload(): Promise<void> {
    if (!confirm('Διακοπή ανεβάσματος; Το πρόχειρο βίντεο θα παραμείνει στη λίστα.')) return;
    this.upload?.cancel();
    this.upload = null;
    this.uploading.set(false);
    this.paused.set(false);
    this.editor.set(false);
    if (this.createdId) {
      // Abandon the multipart upload so its parts stop using the storage allowance.
      try {
        await firstValueFrom(this.api.abortUpload(this.createdId));
      } catch {
        this.note.set('Το ανέβασμα σταμάτησε, αλλά το πρόχειρο χρειάζεται έλεγχο.');
      }
    }
    await this.load();
    await this.loadSummary();
  }

  protected async action(video: Video, action: 'publish' | 'delete' | 'refresh'): Promise<void> {
    if (this.busy() || this.uploading() || this.paused()) return;
    if (
      action === 'delete' &&
      !confirm('Οριστική διαγραφή του βίντεο «' + video.title + '» και του αρχείου του;')
    )
      return;
    if (
      action === 'publish' &&
      !confirm((video.isPublished ? 'Απόσυρση' : 'Δημοσίευση') + ' του «' + video.title + '»;')
    )
      return;
    this.busy.set(true);
    try {
      await firstValueFrom(
        action === 'publish'
          ? this.api.publish(video)
          : action === 'delete'
            ? this.api.delete(video.id)
            : this.api.refresh(video.id),
      );
      await this.load();
      await this.loadTags();
      await this.loadSummary();
    } catch {
      this.note.set('Η ενέργεια δεν ολοκληρώθηκε. Ανανέωσε για να δεις την τρέχουσα κατάσταση.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async move(video: Video, direction: number): Promise<void> {
    const items = this.page()?.items ?? [];
    const index = items.findIndex((x) => x.id === video.id);
    const other = items[index + direction];
    if (!other || this.busy()) return;
    const sorted = [...items];
    [sorted[index], sorted[index + direction]] = [sorted[index + direction], sorted[index]];
    this.busy.set(true);
    try {
      await firstValueFrom(
        this.api.reorder(
          sorted.map((v, i) => ({
            id: v.id,
            revision: v.revision,
            sortOrder: (this.pageNumber - 1) * 12 + i,
          })),
        ),
      );
      await this.load();
    } catch {
      this.note.set('Η σειρά άλλαξε. Ανανέωσε πριν δοκιμάσεις ξανά.');
    } finally {
      this.busy.set(false);
    }
  }

  canLeave(): boolean {
    return (
      (!this.uploading() && !this.paused()) ||
      confirm('Υπάρχει ημιτελές ανέβασμα. Θέλεις να φύγεις;')
    );
  }

  @HostListener('window:beforeunload', ['$event']) protected warn(event: BeforeUnloadEvent): void {
    if (this.uploading() || this.paused()) event.preventDefault();
  }

  ngOnDestroy(): void {
    this.upload?.cancel();
  }
}
