import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import {
  ACCEPTED_IMAGE_TYPES,
  ImageProblem,
  MAX_IMAGE_BYTES,
  ResizedImage,
} from '../../../core/images/image-resize';
import {
  CroppedImage,
  ImageCropData,
  ImageCropDialogComponent,
} from './image-crop-dialog.component';

/** The two shapes this app crops to, and the size each is exported at. */
export const COVER_CROP = { aspectRatio: 16 / 9, round: false, width: 1280 } as const;
export const PORTRAIT_CROP = { aspectRatio: 1, round: true, width: 512 } as const;

/** Either the cropped picture, a reason it was refused, or null when the user backed out. */
export type CropOutcome = ResizedImage | ImageProblem | null;

@Injectable({ providedIn: 'root' })
export class ImageCropService {
  private readonly dialog = inject(MatDialog);

  /**
   * Validates the file, asks the user to choose the visible area, and returns the cropped bytes.
   *
   * The type and size checks are the same two the upload path applied before, done here so a
   * refusal happens before a dialog opens rather than after the user has framed a picture.
   * Resizing is the cropper's `resizeToWidth`, so the picture is cropped and scaled in one pass
   * and only the final bytes exist.
   */
  async crop(
    file: File,
    shape: { aspectRatio: number; round: boolean; width: number },
    text: { title: string; hint: string },
  ): Promise<CropOutcome> {
    if (!(ACCEPTED_IMAGE_TYPES as readonly string[]).includes(file.type)) return 'type';
    if (!file.size || file.size > MAX_IMAGE_BYTES) return 'size';

    const data: ImageCropData = { file, ...shape, ...text };
    const ref = this.dialog.open<ImageCropDialogComponent, ImageCropData, CroppedImage | null>(
      ImageCropDialogComponent,
      {
        data,
        panelClass: 'confirm-panel',
        // The same full-width sheet the confirmation dialog uses on a phone; 100% rather than
        // 100vw, which on iOS includes the safe areas and overflows the page behind it.
        position: isPhone() ? { bottom: '0' } : undefined,
        width: isPhone() ? '100%' : '560px',
        maxWidth: '100%',
        autoFocus: false,
        restoreFocus: true,
        ariaLabelledBy: 'crop-title',
      },
    );

    const result = await firstValueFrom(ref.afterClosed());
    if (!result) return null;

    return {
      blob: result.blob,
      contentType: result.contentType,
      width: shape.width,
      height: Math.round(shape.width / shape.aspectRatio),
    };
  }
}

/** Guarded the same way the confirmation dialog guards it: a webview without matchMedia must not throw. */
function isPhone(): boolean {
  try {
    return typeof window.matchMedia === 'function' && window.matchMedia('(max-width: 599px)').matches;
  } catch {
    return false;
  }
}
