import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, combineLatest, catchError, map, of, switchMap, tap } from 'rxjs';
import { AuthStore } from '../../core/auth/auth.store';
import { SiteContentService } from '../../core/site/site-content.service';
import { BookingButtonComponent } from '../../shared/ui/booking-button/booking-button.component';
import { VideosApi } from '../../core/videos/videos.api';
import {
  AREA_LABELS,
  AUDIENCE_LABELS,
  Tag,
  VideoPage,
  durationLabel,
} from '../../core/videos/video.models';
@Component({
  selector: 'app-video-library',
  imports: [FormsModule, RouterLink, MatIconModule, BookingButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library.component.html',
  styleUrl: './videos.scss',
})
export class VideoLibraryComponent {
  private readonly api = inject(VideosApi);
  /**
   * The trainer browses the same library her clients do, so this is where she notices a title
   * that needs fixing. The pencil only decides what is drawn: the editor's route is admin-only
   * and so is every endpoint behind it.
   */
  protected readonly isAdmin = inject(AuthStore).isAdmin;

  /** Clients are offered a session here, where they are already thinking about training. */
  private readonly site = inject(SiteContentService);
  protected readonly bookingUrl = this.site.bookingUrl;
  protected readonly showBooking = this.site.showBookingInNav;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly page = signal<VideoPage | null>(null);
  protected readonly tags = signal<Tag[]>([]);
  protected readonly busy = signal(true);
  protected readonly failed = signal(false);
  protected readonly areas = AREA_LABELS;
  protected readonly audiences = AUDIENCE_LABELS;
  protected readonly duration = durationLabel;
  private readonly reload = new BehaviorSubject(0);
  protected retry(): void {
    this.reload.next(this.reload.value + 1);
  }
  protected search = '';
  protected audience = '';
  protected bodyArea = '';
  protected equipment = '';
  protected selectedTags: string[] = [];
  constructor() {
    this.api
      .tags()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (t) => this.tags.set(t),
        error: () => {
          /* shared notification */
        },
      });
    combineLatest([this.route.queryParamMap, this.reload])
      .pipe(
        takeUntilDestroyed(),
        map(([p]) => p),
        tap((p) => {
          this.search = p.get('search') ?? '';
          this.audience = p.get('audience') ?? '';
          this.bodyArea = p.get('bodyArea') ?? '';
          this.equipment = p.get('equipment') ?? '';
          this.selectedTags = p.getAll('tags');
          this.busy.set(true);
          this.failed.set(false);
        }),
        switchMap((p) => {
          const params: Record<string, string | number | boolean> = {
            page: Math.min(10000, Math.max(1, Math.floor(Number(p.get('page')) || 1))),
            pageSize: 12,
          };
          for (const key of ['search', 'audience', 'bodyArea', 'equipment']) {
            const value = p.get(key);
            if (value) params[key] = value;
          }
          p.getAll('tags').forEach((id, i) => (params['tags[' + i + ']'] = id));
          return this.api.list(params).pipe(
            catchError(() => {
              this.failed.set(true);
              return of(null);
            }),
          );
        }),
      )
      .subscribe((result) => {
        this.page.set(result);
        this.busy.set(false);
      });
  }
  protected apply(page = 1): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: this.search || null,
        audience: this.audience || null,
        bodyArea: this.bodyArea || null,
        equipment: this.equipment || null,
        tags: this.selectedTags.length ? this.selectedTags : null,
        page,
      },
    });
  }
  protected toggleTag(id: string): void {
    this.selectedTags = this.selectedTags.includes(id)
      ? this.selectedTags.filter((t) => t !== id)
      : [...this.selectedTags, id];
    this.apply();
  }
  protected clear(): void {
    this.search = this.audience = this.bodyArea = this.equipment = '';
    this.selectedTags = [];
    this.apply();
  }
}
