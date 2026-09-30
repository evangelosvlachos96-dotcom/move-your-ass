import { firstValueFrom } from 'rxjs';
import { ImageProblem, MAX_IMAGE_BYTES, ResizedImage } from '../images/image-resize';
import { NotifyService } from '../ui/notify.service';
import { VideosApi } from './videos.api';

/**
 * Stores a cover image against one video.
 *
 * The picture arrives already cropped and scaled by the crop dialog, so what is uploaded is
 * exactly what was framed — and it is small: a phone photo is several megabytes and a few
 * thousand pixels wide, and it is displayed a few hundred wide, so sending the original would
 * spend the storage allowance and every client's data on pixels nobody sees.
 *
 * Returns true when the cover changed. Failures are reported to the user here — the caller only
 * has to decide whether to reload.
 */
export async function uploadCover(
  api: VideosApi,
  notify: NotifyService,
  videoId: string,
  image: ResizedImage,
): Promise<boolean> {
  const ticket = await firstValueFrom(
    api.coverTicket(videoId, image.contentType, image.blob.size),
  );

  if (!(await putBlob(ticket.uploadUrl, image.blob))) {
    notify.error('Η εικόνα δεν ανέβηκε. Δοκίμασε ξανά.');
    return false;
  }

  await firstValueFrom(api.confirmCover(videoId, ticket.objectKey));
  notify.success('Η εικόνα εξωφύλλου ενημερώθηκε.');
  return true;
}

/** Turns a refusal from the crop dialog into the message the trainer should see. */
export function reportImageProblem(notify: NotifyService, problem: ImageProblem): void {
  notify.error(
    problem === 'type'
      ? 'Δεκτές εικόνες: JPG, PNG ή WebP.'
      : problem === 'size'
        ? `Η εικόνα ξεπερνά τα ${Math.round(MAX_IMAGE_BYTES / 1024 / 1024)} MB.`
        : 'Η εικόνα δεν διαβάστηκε. Δοκίμασε άλλο αρχείο.',
  );
}

/**
 * A plain PUT to a presigned URL.
 *
 * XMLHttpRequest rather than fetch, and outside ApiClient on purpose: this request goes to the
 * storage provider, not to our API, so it must carry none of the app's headers or interceptors.
 */
function putBlob(url: string, body: Blob): Promise<boolean> {
  return new Promise((resolve) => {
    const xhr = new XMLHttpRequest();
    xhr.open('PUT', url, true);
    xhr.onload = () => resolve(xhr.status >= 200 && xhr.status < 300);
    xhr.onerror = () => resolve(false);
    xhr.ontimeout = () => resolve(false);
    xhr.send(body);
  });
}
