import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, forkJoin, of, switchMap } from 'rxjs';
import { VideosApi } from '../../core/videos/videos.api';
import { Video, AREA_LABELS, durationLabel } from '../../core/videos/video.models';
@Component({
  selector: 'app-video-player',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './videos.scss',
  template: `<section class="video-page">
    <a [routerLink]="admin ? '/admin/videos' : '/videos'">← Όλες οι προπονήσεις</a>
    @if (failed()) {
      <div class="empty" role="alert">
        <h1>Το βίντεο δεν είναι διαθέσιμο.</h1>
        <p>Ίσως αποσύρθηκε ή δεν έχει ολοκληρωθεί η επεξεργασία του.</p>
        <button (click)="retry()">Επανάληψη</button>
      </div>
    } @else if (video(); as v) {
      <header class="video-heading">
        <div>
          <span class="eyebrow">{{ areas[v.bodyArea] }} · {{ duration(v.durationSeconds) }}</span>
          <h1>{{ v.title }}</h1>
        </div>
      </header>
      <iframe
        class="player"
        [src]="url()"
        [title]="v.title"
        allow="autoplay; fullscreen; picture-in-picture; encrypted-media"
        allowfullscreen
      ></iframe>
      <p class="description">{{ v.description }}</p>
      <div class="tags">
        @for (tag of v.tags; track tag.id) {
          <span>{{ tag.name }}</span>
        }
      </div>
    } @else {
      <p role="status">Φόρτωση βίντεο…</p>
    }
  </section>`,
})
export class VideoPlayerComponent {
  private readonly api = inject(VideosApi);
  private readonly sanitizer = inject(DomSanitizer);
  protected readonly video = signal<Video | null>(null);
  protected readonly url = signal<SafeResourceUrl | null>(null);
  protected readonly failed = signal(false);
  protected readonly areas = AREA_LABELS;
  protected readonly duration = durationLabel;
  private readonly route = inject(ActivatedRoute);
  protected readonly admin = this.route.snapshot.data['admin'] === true;
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
            const url = new URL(playback.url);
            if (
              url.origin !== 'https://iframe.mediadelivery.net' ||
              !url.pathname.startsWith('/embed/')
            ) {
              this.failed.set(true);
              return;
            }
            this.video.set(video);
            this.url.set(this.sanitizer.bypassSecurityTrustResourceUrl(url.href));
          } catch {
            this.failed.set(true);
          }
        },
        error: () => this.failed.set(true),
      });
  }
}
