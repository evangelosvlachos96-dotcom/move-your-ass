/** What the API accepts for a cover or a trainer photo. */
export const ACCEPTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

/** The largest file a person may pick, before anything is done to it. */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

export interface ResizedImage {
  blob: Blob;
  contentType: string;
  width: number;
  height: number;
}

export type ImageProblem = 'type' | 'size' | 'decode';

/**
 * Shrinks a picked image before it is uploaded.
 *
 * A phone camera photo is several thousand pixels wide and several megabytes; as a cover it is
 * displayed a few hundred pixels wide. Sending the original would spend the storage allowance and
 * every client's data on detail nobody sees, so it is drawn into a canvas at a sane width and
 * re-encoded. WebP where the browser supports it, JPEG otherwise — both far smaller than a PNG
 * screenshot, which is the other thing people upload.
 *
 * PNG transparency is deliberately lost: these are photographs behind text, and a JPEG or WebP is
 * a fraction of the size.
 */
export async function resizeImage(
  file: File,
  maxWidth = 1280,
  quality = 0.82,
): Promise<ResizedImage | ImageProblem> {
  if (!(ACCEPTED_IMAGE_TYPES as readonly string[]).includes(file.type)) {
    return 'type';
  }

  if (!file.size || file.size > MAX_IMAGE_BYTES) {
    return 'size';
  }

  const source = await decode(file);
  if (!source) {
    return 'decode';
  }

  try {
    const scale = Math.min(1, maxWidth / source.width);
    const width = Math.max(1, Math.round(source.width * scale));
    const height = Math.max(1, Math.round(source.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d');
    if (!context) {
      return 'decode';
    }

    context.drawImage(source.image, 0, 0, width, height);

    const contentType = supportsWebp(canvas) ? 'image/webp' : 'image/jpeg';
    const blob = await toBlob(canvas, contentType, quality);
    return blob ? { blob, contentType, width, height } : 'decode';
  } finally {
    source.release();
  }
}

/** Square crop for the trainer photo, taken from the centre. */
export async function resizeSquare(file: File, size = 512, quality = 0.85): Promise<ResizedImage | ImageProblem> {
  if (!(ACCEPTED_IMAGE_TYPES as readonly string[]).includes(file.type)) {
    return 'type';
  }

  if (!file.size || file.size > MAX_IMAGE_BYTES) {
    return 'size';
  }

  const source = await decode(file);
  if (!source) {
    return 'decode';
  }

  try {
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const context = canvas.getContext('2d');
    if (!context) {
      return 'decode';
    }

    // Cover, not contain: fill the square and crop the overflow, so a portrait photo does not end
    // up as a tall picture with bars down the sides.
    const side = Math.min(source.width, source.height);
    const sx = (source.width - side) / 2;
    const sy = (source.height - side) / 2;
    context.drawImage(source.image, sx, sy, side, side, 0, 0, size, size);

    const contentType = supportsWebp(canvas) ? 'image/webp' : 'image/jpeg';
    const blob = await toBlob(canvas, contentType, quality);
    return blob ? { blob, contentType, width: size, height: size } : 'decode';
  } finally {
    source.release();
  }
}

interface DecodedImage {
  image: CanvasImageSource;
  width: number;
  height: number;
  release: () => void;
}

/**
 * createImageBitmap where it exists, an <img> element otherwise. Safari only gained the former
 * relatively recently, and this runs on the trainer's phone.
 */
async function decode(file: File): Promise<DecodedImage | null> {
  if (typeof createImageBitmap === 'function') {
    try {
      const bitmap = await createImageBitmap(file);
      return {
        image: bitmap,
        width: bitmap.width,
        height: bitmap.height,
        release: () => bitmap.close(),
      };
    } catch {
      // Fall through to the element, which handles a few formats bitmaps refuse.
    }
  }

  return new Promise<DecodedImage | null>((resolve) => {
    const url = URL.createObjectURL(file);
    const image = new Image();
    image.onload = () =>
      resolve({
        image,
        width: image.naturalWidth,
        height: image.naturalHeight,
        release: () => URL.revokeObjectURL(url),
      });
    image.onerror = () => {
      URL.revokeObjectURL(url);
      resolve(null);
    };
    image.src = url;
  });
}

function supportsWebp(canvas: HTMLCanvasElement): boolean {
  return canvas.toDataURL('image/webp').startsWith('data:image/webp');
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, quality));
}
