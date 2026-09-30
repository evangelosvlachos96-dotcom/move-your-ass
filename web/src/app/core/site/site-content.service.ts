import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthStore } from '../auth/auth.store';
import { SiteApi } from './site.api';
import { About } from './site.models';

/**
 * The trainer's page, loaded once and shared.
 *
 * The shell needs one field of it — the booking link — to decide whether to draw a button in the
 * sidebar and the bottom bar, and the About page needs all of it. Without somewhere shared that
 * would be two requests for the same row on every navigation, and two answers that could disagree
 * for a moment after a save.
 */
@Injectable({ providedIn: 'root' })
export class SiteContentService {
  private readonly api = inject(SiteApi);
  private readonly store = inject(AuthStore);

  private readonly content = signal<About | null>(null);
  private loading = false;

  /**
   * The booking link, or null.
   *
   * Null while nothing is loaded yet, which is the right answer: a button that appears a beat
   * after the page does is worse than one that appears with it.
   */
  readonly bookingUrl = computed(() => this.content()?.bookingUrl ?? null);

  /**
   * Whether to offer booking in the navigation. Clients only — the trainer does not book
   * sessions with herself, and an orange button she can never use is noise on every screen.
   * She still sees it on the About page, where it is labelled as the client's view.
   */
  readonly showBookingInNav = computed(
    () => !this.store.isAdmin() && this.bookingUrl() !== null,
  );

  /** Loads once per session unless something asks for a refresh. Failures are silent: a missing
   * booking button is not worth an error toast on a page that has nothing to do with it. */
  ensureLoaded(): void {
    if (this.content() || this.loading || !this.store.isAuthenticated()) return;
    this.loading = true;
    this.api.about().subscribe({
      next: (about) => {
        this.content.set(about);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  /** Called after a save so the shell's button matches what was just stored. */
  set(about: About): void {
    this.content.set(about);
  }

  /** Called on sign-out: the next person may be somebody else. */
  clear(): void {
    this.content.set(null);
    this.loading = false;
  }
}
