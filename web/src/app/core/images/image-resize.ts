/** The image formats the API accepts for covers and for the trainer photo. */
export const ACCEPTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

/** Mirrors the server's limit, so a file too large is refused before a dialog opens. */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

/** A picture ready to be uploaded: already cropped, scaled and encoded. */
export interface ResizedImage {
  blob: Blob;
  contentType: string;
  width: number;
  height: number;
}

/**
 * Why a picture was refused.
 *
 * `type` and `size` are checked before anything is opened; `decode` comes from the crop dialog,
 * which is the only thing that actually reads the pixels.
 */
export type ImageProblem = 'type' | 'size' | 'decode';
