import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ImageCroppedEvent, ImageCropperComponent, ImageTransform } from 'ngx-image-cropper';

export interface ImageCropData {
  file: File;
  /** Width divided by height. 16/9 for a video cover, 1 for the trainer photo. */
  aspectRatio: number;
  /** Draw the frame as a circle. The export is still a square; only the drawing differs. */
  round: boolean;
  /** Longest edge of the exported image, in pixels. */
  width: number;
  title: string;
  hint: string;
}

/** What the caller gets back: the cropped bytes, and what they were encoded as. */
export interface CroppedImage {
  blob: Blob;
  contentType: string;
}

const MIN_ZOOM = 1;
const MAX_ZOOM = 3;

/**
 * Choose the part of a picture that is actually used.
 *
 * Before this, a cover was the middle of whatever the trainer picked and a portrait was the
 * centre square — so a photo taken in portrait lost its subject's head, and there was nothing to
 * be done about it but crop the file first in another app.
 *
 * The frame is fixed to the shape the destination renders at, which is the point: what is inside
 * the frame is exactly what clients will see, at the moment of choosing rather than afterwards.
 * Dragging and pinching both work — the cropper handles touch — and the slider is there because
 * pinch-zoom is awkward one-handed and impossible with a mouse.
 */
@Component({
  selector: 'app-image-crop-dialog',
  imports: [ImageCropperComponent, FormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './image-crop-dialog.component.html',
  styleUrl: './image-crop-dialog.component.scss',
})
export class ImageCropDialogComponent {
  protected readonly data = inject<ImageCropData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ImageCropDialogComponent, CroppedImage | null>);

  protected readonly ready = signal(false);
  protected readonly failed = signal(false);
  protected readonly busy = signal(false);
  /** Preview of the current frame, so the trainer sees the result and not only the selection. */
  protected readonly preview = signal<string | null>(null);

  protected readonly minZoom = MIN_ZOOM;
  protected readonly maxZoom = MAX_ZOOM;
  protected zoom = MIN_ZOOM;
  protected transform: ImageTransform = { scale: MIN_ZOOM };

  /**
   * WebP where the browser can encode it, JPEG otherwise. Decided once here rather than trusting
   * the cropper's default, which is PNG — a photograph as PNG is several times the size for no
   * visible gain, and it would be spent out of the storage allowance.
   */
  protected readonly format: 'webp' | 'jpeg' = canEncodeWebp() ? 'webp' : 'jpeg';

  private cropped: Blob | null = null;

  protected onZoom(value: number): void {
    this.zoom = value;
    this.transform = { ...this.transform, scale: value };
  }

  protected onCropped(event: ImageCroppedEvent): void {
    this.cropped = event.blob ?? null;
    const previous = this.preview();
    if (previous) URL.revokeObjectURL(previous);
    this.preview.set(event.blob ? URL.createObjectURL(event.blob) : null);
  }

  protected onLoaded(): void {
    this.ready.set(true);
  }

  protected onFailed(): void {
    this.failed.set(true);
  }

  protected confirm(): void {
    if (!this.cropped || this.busy()) return;
    this.busy.set(true);
    this.ref.close({ blob: this.cropped, contentType: `image/${this.format}` });
  }

  protected cancel(): void {
    this.ref.close(null);
  }
}

/**
 * Whether toDataURL('image/webp') really produces WebP.
 *
 * A browser that cannot encode it silently returns a PNG data URL instead of failing, so the
 * only reliable check is to look at what came back.
 */
function canEncodeWebp(): boolean {
  try {
    const canvas = document.createElement('canvas');
    canvas.width = 1;
    canvas.height = 1;
    return canvas.toDataURL('image/webp').startsWith('data:image/webp');
  } catch {
    return false;
  }
}
