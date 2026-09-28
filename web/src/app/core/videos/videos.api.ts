import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../http/api-client.service';
import {
  CoverTicket,
  CreatedVideo,
  Playback,
  Tag,
  UploadRequest,
  UploadTicket,
  Video,
  VideoInput,
  VideoPage,
  VideoSummary,
} from './video.models';
@Injectable({ providedIn: 'root' })
export class VideosApi {
  private readonly api = inject(ApiClient);
  list(params: Record<string, string | number | boolean>, admin = false) {
    return this.api.get<VideoPage>(admin ? '/admin/videos' : '/videos', { params });
  }
  detail(id: string, admin = false) {
    return this.api.get<Video>((admin ? '/admin/videos/' : '/videos/') + id);
  }
  tags(admin = false) {
    return this.api.get<Tag[]>(admin ? '/admin/tags' : '/videos/filters');
  }
  create(video: VideoInput, file: UploadRequest, key: string) {
    return this.api.post<CreatedVideo>(
      '/admin/videos',
      { video, file },
      { headers: { 'Idempotency-Key': key } },
    );
  }
  update(id: string, input: VideoInput) {
    return this.api.put<void>('/admin/videos/' + id, input);
  }
  upload(id: string, file: UploadRequest) {
    return this.api.post<UploadTicket>('/admin/videos/' + id + '/upload', file);
  }
  completeUpload(id: string, thumbnailUploaded: boolean, durationSeconds: number | null) {
    return this.api.post<void>('/admin/videos/' + id + '/upload/complete', {
      thumbnailUploaded,
      durationSeconds,
    });
  }
  abortUpload(id: string) {
    return this.api.post<void>('/admin/videos/' + id + '/upload/abort');
  }
  coverTicket(id: string, contentType: string, sizeBytes: number) {
    return this.api.post<CoverTicket>('/admin/videos/' + id + '/cover', { contentType, sizeBytes });
  }
  confirmCover(id: string, objectKey: string) {
    return this.api.put<void>('/admin/videos/' + id + '/cover', { objectKey });
  }
  removeCover(id: string) {
    return this.api.delete<void>('/admin/videos/' + id + '/cover');
  }
  publish(video: Video) {
    return this.api.post<void>(
      '/admin/videos/' + video.id + (video.isPublished ? '/unpublish' : '/publish'),
      { revision: video.revision },
    );
  }
  refresh(id: string) {
    return this.api.post<void>('/admin/videos/' + id + '/refresh');
  }
  delete(id: string) {
    return this.api.delete<void>('/admin/videos/' + id);
  }
  addTag(name: string) {
    return this.api.post<Tag>('/admin/tags', { name });
  }
  deleteTag(id: string) {
    return this.api.delete<void>('/admin/tags/' + id);
  }
  reorder(items: { id: string; sortOrder: number; revision: string }[]) {
    return this.api.post<void>('/admin/videos/reorder', items);
  }
  playback(id: string, admin = false) {
    return this.api.get<Playback>((admin ? '/admin/videos/' : '/videos/') + id + '/playback');
  }
  summary() {
    return this.api.get<VideoSummary>('/admin/videos/summary');
  }
}
