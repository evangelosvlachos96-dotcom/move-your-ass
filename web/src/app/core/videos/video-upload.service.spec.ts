import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { VideoUploadService } from './video-upload.service';
import { UploadTicket } from './video.models';

/**
 * A stand-in for XMLHttpRequest that records every PUT. The uploader talks to object storage
 * directly, so there is no Angular HttpClient here to intercept.
 */
interface Recorded {
  url: string;
  body: Blob;
}

class FakeXhr {
  static sent: Recorded[] = [];
  static statusFor: (call: number, url: string) => number | 'network' = () => 200;
  static instances: FakeXhr[] = [];

  readonly upload = { onprogress: null as ((e: { loaded: number }) => void) | null };
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  ontimeout: (() => void) | null = null;
  onabort: (() => void) | null = null;
  status = 0;
  aborted = false;
  private url = '';

  constructor() {
    FakeXhr.instances.push(this);
  }

  open(_method: string, url: string) {
    this.url = url;
  }

  send(body: Blob) {
    const outcome = FakeXhr.statusFor(FakeXhr.sent.length, this.url);
    FakeXhr.sent.push({ url: this.url, body });
    queueMicrotask(() => {
      if (this.aborted) return;
      this.upload.onprogress?.({ loaded: body.size });
      if (outcome === 'network') {
        this.onerror?.();
        return;
      }
      this.status = outcome;
      this.onload?.();
    });
  }

  abort() {
    this.aborted = true;
    this.onabort?.();
  }
}

function ticket(partCount: number, partSizeBytes = 8, uploadedParts: number[] = []): UploadTicket {
  return {
    partSizeBytes,
    partCount,
    parts: Array.from({ length: partCount }, (_, i) => ({
      partNumber: i + 1,
      url: `https://storage.test/videos/x.mp4?partNumber=${i + 1}&X-Amz-Signature=test`,
    })),
    uploadedParts,
    thumbnailUploadUrl: 'https://storage.test/videos/x-poster.jpg?X-Amz-Signature=test',
  };
}

function file(bytes: number): File {
  return new File([new Uint8Array(bytes)], 'workout.mp4', { type: 'video/mp4' });
}

/**
 * Drains queued parts and any retry backoff. Timers are faked so the retry schedule (up to 15 s
 * between attempts) costs no wall-clock time in the suite.
 */
const settle = (ms = 60000) => vi.advanceTimersByTimeAsync(ms);

describe('VideoUploadService', () => {
  let service: VideoUploadService;

  beforeEach(() => {
    FakeXhr.sent = [];
    FakeXhr.instances = [];
    FakeXhr.statusFor = () => 200;
    vi.useFakeTimers();
    vi.stubGlobal('XMLHttpRequest', FakeXhr);
    service = TestBed.inject(VideoUploadService);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('splits the file into one PUT per part and reports completion', async () => {
    const done = vi.fn();
    const failed = vi.fn();
    const progress = vi.fn();

    service.start(file(20), ticket(3), { progress, done, failed });
    await settle();

    expect(FakeXhr.sent).toHaveLength(3);
    expect(FakeXhr.sent.map((r) => r.body.size)).toEqual([8, 8, 4]);
    expect(FakeXhr.sent[0].url).toContain('partNumber=1');
    expect(done).toHaveBeenCalledOnce();
    expect(failed).not.toHaveBeenCalled();
    expect(progress).toHaveBeenLastCalledWith(100);
  });

  it('skips parts the provider already holds, so reopening a draft resumes', async () => {
    const done = vi.fn();
    service.start(file(20), ticket(3, 8, [1, 2]), { progress: vi.fn(), done, failed: vi.fn() });
    await settle();

    expect(FakeXhr.sent).toHaveLength(1);
    expect(FakeXhr.sent[0].url).toContain('partNumber=3');
    expect(done).toHaveBeenCalledOnce();
  });

  it('retries only the part that failed rather than the whole recording', async () => {
    let firstAttemptOfPartTwo = true;
    FakeXhr.statusFor = (_call, url) => {
      if (url.includes('partNumber=2') && firstAttemptOfPartTwo) {
        firstAttemptOfPartTwo = false;
        return 'network';
      }
      return 200;
    };
    const done = vi.fn();
    const failed = vi.fn();

    service.start(file(16), ticket(2), { progress: vi.fn(), done, failed });
    await settle();

    // Part 1 once, part 2 twice: the dropped part cost one part, not the file.
    expect(FakeXhr.sent.filter((r) => r.url.includes('partNumber=1'))).toHaveLength(1);
    expect(FakeXhr.sent.filter((r) => r.url.includes('partNumber=2'))).toHaveLength(2);
    expect(failed).not.toHaveBeenCalled();
    expect(done).toHaveBeenCalledOnce();
  });

  it('reports an expired upload link with advice instead of a generic error', async () => {
    FakeXhr.statusFor = () => 403;
    const failed = vi.fn();
    const done = vi.fn();

    service.start(file(8), ticket(1), { progress: vi.fn(), done, failed });
    await settle();

    // Five attempts, then give up with the advice that matches the real cause.
    expect(FakeXhr.sent).toHaveLength(5);
    expect(failed).toHaveBeenCalledOnce();
    expect(failed.mock.calls[0][0]).toContain('έληξε');
    expect(done).not.toHaveBeenCalled();
  });

  it('stops sending further parts once paused', async () => {
    const done = vi.fn();
    const handle = service.start(file(40), ticket(5), {
      progress: vi.fn(),
      done,
      failed: vi.fn(),
    });
    handle.pause();
    await settle();

    expect(FakeXhr.sent.length).toBeLessThan(5);
    expect(done).not.toHaveBeenCalled();
  });

  it('reports a failed thumbnail upload rather than throwing', async () => {
    FakeXhr.statusFor = () => 500;
    const rejected = service.uploadThumbnail('https://storage.test/p.jpg', new Blob(['x']));
    await settle(0);
    expect(await rejected).toBe(false);

    FakeXhr.statusFor = () => 200;
    const accepted = service.uploadThumbnail('https://storage.test/p.jpg', new Blob(['x']));
    await settle(0);
    expect(await accepted).toBe(true);
  });
});
