import { TestBed } from '@angular/core/testing';
import { describe, it, expect, vi } from 'vitest';
import { VideoUploadService } from './video-upload.service';
describe('Direct upload destination', () => {
  it('rejects credentials pointing at an untrusted endpoint before sending a file', () => {
    const service = TestBed.inject(VideoUploadService);
    expect(() =>
      service.start(
        new File(['test'], 'video.mp4', { type: 'video/mp4' }),
        'Title',
        {
          endpoint: 'https://untrusted.example/tusupload',
          videoId: 'id',
          libraryId: '1',
          signature: 'test',
          expires: 1,
        },
        vi.fn(),
        vi.fn(),
        vi.fn(),
      ),
    ).toThrow();
  });
});
