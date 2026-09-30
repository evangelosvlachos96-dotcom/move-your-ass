import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { VideosApi } from '../../core/videos/videos.api';
import {
  STATUS_LABELS,
  StorageUsage,
  Tag,
  Video,
  VideoPage,
  sizeLabel,
} from '../../core/videos/video.models';
import { ConfirmDialogService } from '../../shared/ui/confirm-dialog/confirm-dialog.service';

/** Videos per page. Also the offset step when the whole page is re-sorted. */
const PAGE_SIZE = 12;

/**
 * The trainer's own list of workouts.
 *
 * One card per video, and one obvious action on it — publish, or withdraw. Everything else lives
 * behind the card's own menu. The previous version put ten buttons in a row under each title,
 * which on a phone wrapped into a block of controls with no visible boundary between one video
 * and the next; it was impossible to tell at a glance where a workout began, let alone which
 * "Διαγραφή" belonged to which.
 *
 * Editing is a page of its own (admin-video-editor), not a form that unfolds in here.
 */
@Component({
  selector: 'app-admin-videos',
  imports: [FormsModule, RouterLink, MatIconModule, MatMenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-videos.component.html',
  styleUrls: ['./videos.scss', './admin-videos.scss'],
})
export class AdminVideosComponent {
  private readonly api = inject(VideosApi);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly page = signal<VideoPage | null>(null);
  protected readonly tags = signal<Tag[]>([]);
  protected readonly busy = signal(false);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly note = signal('');
  protected readonly configured = signal(false);
  protected readonly storage = signal<StorageUsage | null>(null);

  protected readonly status = STATUS_LABELS;
  protected readonly size = sizeLabel;
  protected search = '';
  protected pageNumber = 1;

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

  protected async load(page = this.pageNumber): Promise<void> {
    this.pageNumber = page;
    this.loading.set(true);
    this.failed.set(false);
    try {
      this.page.set(
        await firstValueFrom(this.api.list({ page, pageSize: PAGE_SIZE, search: this.search }, true)),
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

  /** The one line of facts under a title: how long, how big, when it was added. */
  protected meta(video: Video): string {
    const parts: string[] = [];
    if (video.durationSeconds) parts.push(duration(video.durationSeconds));
    if (video.sizeBytes) parts.push(sizeLabel(video.sizeBytes));
    parts.push(new Date(video.createdAtUtc).toLocaleDateString('el-GR'));
    return parts.join(' · ');
  }

  protected async removeTag(tag: Tag): Promise<void> {
    if (tag.usageCount) return;
    const ok = await this.confirmDialog.confirm({
      title: 'Διαγραφή ετικέτας',
      message: `Να διαγραφεί η ετικέτα «${tag.name}»;`,
      confirmLabel: 'Διαγραφή',
      destructive: true,
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.api.deleteTag(tag.id));
      await this.loadTags();
    } catch {
      this.note.set('Η ετικέτα χρησιμοποιείται ή άλλαξε. Ανανέωσε τη λίστα.');
    }
  }

  protected async action(video: Video, action: 'publish' | 'delete' | 'refresh'): Promise<void> {
    if (this.busy()) return;

    if (action === 'delete') {
      const ok = await this.confirmDialog.confirm({
        title: 'Οριστική διαγραφή βίντεο',
        message: `Να διαγραφεί οριστικά το βίντεο «${video.title}»;`,
        detail: 'Διαγράφεται και το αρχείο. Κράτα πρώτα το δικό σου αντίγραφο — δεν επαναφέρεται.',
        confirmLabel: 'Οριστική διαγραφή',
        destructive: true,
      });
      if (!ok) return;
    }

    if (action === 'publish') {
      const withdrawing = video.isPublished;
      const ok = await this.confirmDialog.confirm({
        title: withdrawing ? 'Απόσυρση βίντεο' : 'Δημοσίευση βίντεο',
        message: withdrawing
          ? `Να αποσυρθεί το «${video.title}» από τη βιβλιοθήκη;`
          : `Να δημοσιευτεί το «${video.title}» σε όλους τους πελάτες;`,
        detail: withdrawing
          ? 'Δεν θα εκδίδονται νέοι σύνδεσμοι. Ένας σύνδεσμος που έχει ήδη δοθεί δουλεύει μέχρι να λήξει.'
          : undefined,
        confirmLabel: withdrawing ? 'Απόσυρση' : 'Δημοσίευση',
        destructive: withdrawing,
      });
      if (!ok) return;
    }

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
            sortOrder: (this.pageNumber - 1) * PAGE_SIZE + i,
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
}

/** m:ss, which is how a workout's length is read. */
function duration(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const rest = Math.round(seconds % 60);
  return `${minutes}:${rest.toString().padStart(2, '0')}`;
}
