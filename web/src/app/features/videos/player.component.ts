import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, forkJoin, of, switchMap } from 'rxjs';
import { AuthStore } from '../../core/auth/auth.store';
import { VideosApi } from '../../core/videos/videos.api';
import { Video, AREA_LABELS, durationLabel } from '../../core/videos/video.models';

/**
 * Plays the original recording from a short-lived presigned URL in a native video element
 * (ADR-019). Object storage does no transcoding and offers no player, so the browser plays the
 * file the trainer uploaded and seeks with HTTP range requests.
 */
@Component({
  selector: 'app-video-player',
  imports: [SkeletonComponent, RouterLink, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './videos.scss',
  template: `<section class="video-page plain-form">
    <a class="button back-link" [routerLink]="admin ? '/admin/videos' : '/videos'"
      ><mat-icon aria-hidden="true">arrow_back</mat-icon><span>Όλες οι προπονήσεις</span></a
    >
    @if (failed()) {
      <div class="empty" role="alert">
        <h1>Το βίντεο δεν είναι διαθέσιμο.</h1>
        <p>Ίσως αποσύρθηκε, ή ο σύνδεσμος αναπαραγωγής έληξε.</p>
        <button (click)="retry()">Επανάληψη</button>
      </div>
    } @else if (video(); as v) {
      <header class="video-heading">
        <div>
          <span class="eyebrow">{{ areas[v.bodyArea] }} · {{ duration(v.durationSeconds) }}</span>
          <h1>{{ v.title }}</h1>
        </div>
        @if (isAdmin()) {
          <a
            class="button"
            [routerLink]="['/admin/videos', v.id, 'edit']"
            [queryParams]="{ returnUrl: currentUrl }"
          >
            <mat-icon aria-hidden="true">edit</mat-icon>
            <span>Επεξεργασία</span>
          </a>
        }
      </header>
      <video
        class="player"
        [src]="url()"
        [poster]="poster() ?? undefined"
        [title]="v.title"
        controls
        playsinline
        preload="metadata"
        controlsList="nodownload"
        (error)="failed.set(true)"
      ></video>
      <p class="description">{{ v.description }}</p>
      <div class="tags">
        @for (tag of v.tags; track tag.id) {
          <span>{{ tag.name }}</span>
        }
      </div>
    } @else {
      <app-skeleton variant="player" label="Φόρτωση βίντεο…" />
    }
  </section>`,
})
export class VideoPlayerComponent {
  private readonly api = inject(VideosApi);
  protected readonly video = signal<Video | null>(null);
  protected readonly url = signal<string | null>(null);
  protected readonly poster = signal<string | null>(null);
  protected readonly failed = signal(false);
  protected readonly areas = AREA_LABELS;
  protected readonly duration = durationLabel;
  private readonly route = inject(ActivatedRoute);
  protected readonly admin = this.route.snapshot.data['admin'] === true;
  protected readonly isAdmin = inject(AuthStore).isAdmin;

  /** Where the editor returns to, so saving from here comes back to this video, not the list. */
  protected get currentUrl(): string {
    return (this.admin ? '/admin/videos/' : '/videos/') + this.route.snapshot.paramMap.get('id');
  }
  private readonly reload = new BehaviorSubject(0);

  protected retry(): void {
    this.reload.next(this.reload.value + 1);
  }

  constructor() {
    const route = this.route;
    combineLatest([route.paramMap, this.reload])
      .pipe(
        takeUntilDestroyed(),
        switchMap(([p]) => {
          this.failed.set(false);
          this.video.set(null);
          this.url.set(null);
          this.poster.set(null);
          const id = p.get('id')!;
          const admin = route.snapshot.data['admin'] === true;
          return forkJoin({
            video: this.api.detail(id, admin),
            playback: this.api.playback(id, admin),
          }).pipe(
            catchError(() => {
              this.failed.set(true);
              return of(null);
            }),
          );
        }),
      )
      .subscribe({
        next: (result) => {
          if (!result) return;
          const { video, playback } = result;
          try {
            // Only ever play an https URL the API handed us; never a value from the page.
            const url = new URL(playback.url);
            if (url.protocol !== 'https:') {
              this.failed.set(true);
              return;
            }
            this.video.set(video);
            this.url.set(url.href);
            // The detail response already carries a presigned poster URL, or null for none.
            this.poster.set(video.thumbnailUrl);
          } catch {
            this.failed.set(true);
          }
        },
        error: () => this.failed.set(true),
      });
  }
}
