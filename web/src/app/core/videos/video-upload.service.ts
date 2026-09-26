import { Injectable } from '@angular/core';
import { Upload } from 'tus-js-client';
import { UploadCredentials } from './video.models';
@Injectable({ providedIn: 'root' })
export class VideoUploadService {
  start(
    file: File,
    title: string,
    c: UploadCredentials,
    progress: (value: number) => void,
    done: () => void,
    failed: () => void,
  ): Upload {
    if (c.endpoint !== 'https://video.bunnycdn.com/tusupload')
      throw new Error('Invalid upload destination');
    const upload = new Upload(file, {
      endpoint: c.endpoint,
      chunkSize: 8 * 1024 * 1024,
      retryDelays: [0, 3000, 5000, 10000, 20000],
      storeFingerprintForResuming: false,
      headers: {
        AuthorizationSignature: c.signature,
        AuthorizationExpire: String(c.expires),
        LibraryId: c.libraryId,
        VideoId: c.videoId,
      },
      metadata: { filetype: file.type || 'video/mp4', title },
      onProgress: (sent, total) => progress(Math.round((sent / total) * 100)),
      onSuccess: done,
      onError: () => failed(),
    });
    upload.start();
    return upload;
  }
}
