import { Injectable } from '@angular/core';
import { UploadTicket } from './video.models';

/** Progress and lifecycle callbacks for one upload. */
export interface UploadHandlers {
  progress: (percent: number) => void;
  done: () => void;
  failed: (message: string) => void;
}

/** Cancels or pauses an upload in flight. Both stop after the part currently in the air. */
export interface UploadHandle {
  pause(): void;
  cancel(): void;
}

const RETRY_DELAYS_MS = [0, 1000, 3000, 7000, 15000];

/**
 * Uploads a file straight to S3-compatible object storage, one presigned part at a time
 * (ADR-019). Parts are independent: a dropped connection costs the part in flight, not the
 * whole recording, which is what makes a phone on mobile data survive the upload.
 *
 * The browser never sees a provider credential — only presigned URLs the API issued — and it
 * never reads a part's ETag, because the server completes the upload from its own ListParts.
 */
@Injectable({ providedIn: 'root' })
export class VideoUploadService {
  /**
   * Starts (or resumes) an upload. Parts listed in `ticket.uploadedParts` are already stored and
   * are skipped, so reopening a draft continues instead of starting over.
   */
  start(file: File, ticket: UploadTicket, handlers: UploadHandlers): UploadHandle {
    const done = new Set(ticket.uploadedParts);
    const pending = ticket.parts.filter((part) => !done.has(part.partNumber));
    let stopped: 'paused' | 'cancelled' | null = null;
    let current: XMLHttpRequest | null = null;
    let sentBefore = done.size * ticket.partSizeBytes;

    const total = file.size || 1;
    const report = (bytesInFlight: number) =>
      handlers.progress(Math.min(100, Math.round(((sentBefore + bytesInFlight) / total) * 100)));

    report(0);

    const run = async () => {
      for (const part of pending) {
        if (stopped) {
          return;
        }

        const start = (part.partNumber - 1) * ticket.partSizeBytes;
        const slice = file.slice(start, Math.min(start + ticket.partSizeBytes, file.size));

        let sent = false;
        for (let attempt = 0; attempt < RETRY_DELAYS_MS.length && !sent; attempt++) {
          if (attempt > 0) {
            await delay(RETRY_DELAYS_MS[attempt]);
            if (stopped) {
              return;
            }
          }

          try {
            await this.put(part.url, slice, (bytes) => report(bytes), (xhr) => (current = xhr));
            sent = true;
          } catch (error) {
            if (stopped) {
              return;
            }
            if (attempt === RETRY_DELAYS_MS.length - 1) {
              handlers.failed(messageFor(error));
              return;
            }
          } finally {
            current = null;
          }
        }

        sentBefore += slice.size;
        report(0);
      }

      handlers.done();
    };

    void run();

    return {
      pause: () => {
        stopped = 'paused';
        current?.abort();
      },
      cancel: () => {
        stopped = 'cancelled';
        current?.abort();
      },
    };
  }

  /**
   * Stores the poster frame at its presigned URL. Best effort: a browser that cannot decode the
   * recording simply produces no frame, and the library falls back to the branded placeholder.
   */
  async uploadThumbnail(url: string, frame: Blob): Promise<boolean> {
    try {
      await this.put(url, frame, ignore, ignore);
      return true;
    } catch {
      return false;
    }
  }

  private put(
    url: string,
    body: Blob,
    onProgress: (bytes: number) => void,
    onStart: (xhr: XMLHttpRequest) => void,
  ): Promise<void> {
    return new Promise<void>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open('PUT', url, true);
      xhr.upload.onprogress = (event) => onProgress(event.loaded);
      xhr.onload = () =>
        xhr.status >= 200 && xhr.status < 300
          ? resolve()
          : reject(new UploadError(`HTTP ${xhr.status}`, xhr.status));
      xhr.onerror = () => reject(new UploadError('network'));
      xhr.ontimeout = () => reject(new UploadError('timeout'));
      xhr.onabort = () => reject(new UploadError('aborted'));
      onStart(xhr);
      xhr.send(body);
    });
  }
}

/**
 * Captures a poster frame from the selected file, plus its duration. Returns nulls rather than
 * throwing when the browser cannot decode the recording, which happens for some phone codecs.
 */
export function captureFrame(file: File): Promise<{ frame: Blob | null; duration: number | null }> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(file);
    const video = document.createElement('video');
    let settled = false;

    const finish = (frame: Blob | null, duration: number | null) => {
      if (settled) return;
      settled = true;
      URL.revokeObjectURL(url);
      video.removeAttribute('src');
      resolve({ frame, duration });
    };

    // Some phone recordings never fire a usable frame; do not hold the upload for them.
    const timer = setTimeout(() => finish(null, null), 8000);

    video.muted = true;
    video.playsInline = true;
    video.preload = 'metadata';
    video.onerror = () => {
      clearTimeout(timer);
      finish(null, null);
    };
    video.onloadedmetadata = () => {
      const duration = Number.isFinite(video.duration) ? Math.round(video.duration) : null;
      // One second in avoids the black first frame most recordings start on.
      video.currentTime = Math.min(1, Math.max(0, (video.duration || 0) / 2));
      video.onseeked = () => {
        clearTimeout(timer);
        const canvas = document.createElement('canvas');
        const scale = Math.min(1, 640 / (video.videoWidth || 640));
        canvas.width = Math.round((video.videoWidth || 640) * scale);
        canvas.height = Math.round((video.videoHeight || 360) * scale);
        const context = canvas.getContext('2d');
        if (!context || !canvas.width || !canvas.height) {
          finish(null, duration);
          return;
        }
        context.drawImage(video, 0, 0, canvas.width, canvas.height);
        canvas.toBlob((blob) => finish(blob, duration), 'image/jpeg', 0.72);
      };
    };
    video.src = url;
  });
}

class UploadError extends Error {
  constructor(
    message: string,
    readonly status?: number,
  ) {
    super(message);
    this.name = 'UploadError';
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** A thumbnail upload reports nothing: it is one small object, and failure is not fatal. */
function ignore(): void {
  return undefined;
}

function messageFor(error: unknown): string {
  const status = error instanceof UploadError ? error.status : undefined;
  if (status === 403) {
    return 'Ο σύνδεσμος ανεβάσματος έληξε. Άνοιξε ξανά το βίντεο και συνέχισε το ανέβασμα.';
  }
  return 'Το ανέβασμα διακόπηκε. Έλεγξε τη σύνδεσή σου και πάτησε «Συνέχεια».';
}
