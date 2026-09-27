import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SessionSyncService } from './session-sync.service';

/** A Web Locks stand-in that actually serialises, so "second caller waits" is really tested. */
function fakeLocks() {
  let chain: Promise<unknown> = Promise.resolve();
  return {
    request: vi.fn(<T>(_name: string, callback: () => Promise<T>): Promise<T> => {
      const run = chain.then(() => callback());
      chain = run.catch(() => undefined);
      return run;
    }),
  };
}

function install(locks: unknown): void {
  Object.defineProperty(navigator, 'locks', { value: locks, configurable: true });
}

describe('SessionSyncService', () => {
  let service: SessionSyncService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    service = TestBed.inject(SessionSyncService);
  });

  afterEach(() => {
    Reflect.deleteProperty(navigator, 'locks');
    vi.useRealTimers();
  });

  it('refreshes once and lends the token to whoever was waiting', async () => {
    install(fakeLocks());
    const refresh = vi.fn().mockResolvedValue('token-1');

    const [first, second] = await Promise.all([
      service.coordinate(refresh),
      service.coordinate(refresh),
    ]);

    // The point of the lock: the second caller reuses the first result rather than presenting
    // the same cookie again, which the server would be entitled to read as token theft.
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(first).toBe('token-1');
    expect(second).toBe('token-1');
  });

  it('refreshes again once the shared token is no longer fresh', async () => {
    vi.useFakeTimers();
    install(fakeLocks());
    const refresh = vi.fn().mockResolvedValueOnce('token-1').mockResolvedValueOnce('token-2');

    expect(await service.coordinate(refresh)).toBe('token-1');
    vi.advanceTimersByTime(60_000);
    expect(await service.coordinate(refresh)).toBe('token-2');

    expect(refresh).toHaveBeenCalledTimes(2);
  });

  it('still refreshes where the Web Locks API is absent', async () => {
    Reflect.deleteProperty(navigator, 'locks');
    const refresh = vi.fn().mockResolvedValue('token-1');

    expect(await service.coordinate(refresh)).toBe('token-1');
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('still refreshes where the browser advertises locks but refuses the request', async () => {
    // Some private-browsing modes do exactly this.
    install({ request: vi.fn().mockRejectedValue(new Error('denied')) });
    const refresh = vi.fn().mockResolvedValue('token-1');

    expect(await service.coordinate(refresh)).toBe('token-1');
  });

  it('propagates a refresh failure rather than swallowing it into a broken session', async () => {
    install(fakeLocks());
    const refresh = vi.fn().mockRejectedValue(new Error('401'));

    await expect(service.coordinate(refresh)).rejects.toThrow('401');
  });
});
