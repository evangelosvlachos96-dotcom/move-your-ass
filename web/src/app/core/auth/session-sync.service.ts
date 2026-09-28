import { Injectable, OnDestroy } from '@angular/core';

/** An access token one tab obtained, shared with the others. */
interface SharedToken {
  kind: 'token';
  accessToken: string;
  at: number;
}

/** One tab signed out; the others must not stay half-authenticated. */
interface SignedOut {
  kind: 'signed-out';
  at: number;
}

type SyncMessage = SharedToken | SignedOut;

const LOCK_NAME = 'mya-auth-refresh';
const CHANNEL_NAME = 'mya-auth';

/**
 * A token another tab produced this recently is still worth using instead of refreshing again.
 * Comfortably shorter than the access token's fifteen minutes, and comfortably longer than the
 * time between two tabs waking from a background throttle at the same moment.
 */
const FRESH_MS = 10_000;

/**
 * Makes refresh single-flight **across tabs**, not just within one.
 *
 * Each tab already shares one in-flight refresh internally, but two tabs are two JavaScript
 * contexts sharing one cookie jar. Both read the same refresh cookie, both POST it, and the
 * loser presents a token the winner has already rotated — which the server is entitled to read
 * as token theft. The server now forgives that inside a grace window; this stops it happening in
 * the first place, which is better than being forgiven.
 *
 * The Web Locks API serialises the tabs. Whoever holds the lock refreshes and broadcasts the new
 * access token; whoever was waiting finds a fresh token already there and uses it without a
 * second request.
 *
 * Neither API is assumed: without `navigator.locks` (or in a context where it throws) this
 * degrades to the per-tab behaviour, which is what the server's grace window is there for.
 */
@Injectable({ providedIn: 'root' })
export class SessionSyncService implements OnDestroy {
  private readonly channel = this.openChannel();
  private shared: SharedToken | null = null;
  private onSignedOut: (() => void) | null = null;

  constructor() {
    if (this.channel) {
      this.channel.onmessage = (event: MessageEvent<SyncMessage>) => {
        const message = event.data;
        if (message?.kind === 'token' && typeof message.accessToken === 'string') {
          this.shared = message;
          return;
        }

        if (message?.kind === 'signed-out') {
          // A token borrowed from before the sign-out is no longer worth anything.
          this.shared = null;
          this.onSignedOut?.();
        }
      };
    }
  }

  /**
   * Called once by AuthService so a sign-out in any tab reaches the rest. A callback rather than
   * an observable because there is exactly one listener and it lives for the life of the app.
   */
  whenSignedOutElsewhere(handler: () => void): void {
    this.onSignedOut = handler;
  }

  /** Tells the other tabs that this session is over. Also forgets any token in flight here. */
  announceSignOut(): void {
    this.shared = null;
    this.channel?.postMessage({ kind: 'signed-out', at: Date.now() } satisfies SignedOut);
  }

  /**
   * Runs `refresh` under a cross-tab lock, skipping it entirely if another tab refreshed while
   * this one was queued. Returns the access token that ended up being current.
   */
  async coordinate(refresh: () => Promise<string>): Promise<string> {
    const locks = this.locks();
    if (!locks) {
      return refresh();
    }

    try {
      return await locks.request(LOCK_NAME, async () => {
        const borrowed = this.fresh();
        if (borrowed) {
          return borrowed;
        }

        const token = await refresh();
        this.publish(token);
        return token;
      });
    } catch {
      // A browser that advertises the API but refuses the request (some private modes) must not
      // take the session down with it.
      return refresh();
    }
  }

  /** A token another tab published moments ago, or null. */
  private fresh(): string | null {
    if (this.shared && Date.now() - this.shared.at < FRESH_MS) {
      return this.shared.accessToken;
    }

    return null;
  }

  private publish(accessToken: string): void {
    const message: SharedToken = { kind: 'token', accessToken, at: Date.now() };
    this.shared = message;
    this.channel?.postMessage(message);
  }

  private openChannel(): BroadcastChannel | null {
    try {
      return typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(CHANNEL_NAME);
    } catch {
      return null;
    }
  }

  private locks(): LockManager | null {
    try {
      return typeof navigator !== 'undefined' && 'locks' in navigator ? navigator.locks : null;
    } catch {
      return null;
    }
  }

  ngOnDestroy(): void {
    this.channel?.close();
  }
}
