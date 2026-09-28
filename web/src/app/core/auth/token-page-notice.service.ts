import { Injectable } from '@angular/core';

/**
 * Carries one fact from the sign-out guard to the page it guards: "a session was ended to get
 * you here". A guard cannot put anything in the router's navigation state, and the alternative —
 * a query parameter — would survive a reload and a bookmark, and say it again when it is no
 * longer true.
 *
 * Read once and cleared, so a later visit to the same page does not repeat a stale notice.
 */
@Injectable({ providedIn: 'root' })
export class TokenPageNotice {
  private pending = false;

  /** Called by the guard after it ends a session. */
  raise(): void {
    this.pending = true;
  }

  /** Called by the page as it renders. True at most once per sign-out. */
  consume(): boolean {
    const raised = this.pending;
    this.pending = false;
    return raised;
  }
}
