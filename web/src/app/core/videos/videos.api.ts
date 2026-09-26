import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../http/api-client.service';
import {
  CreatedVideo,
  Playback,
  Tag,
  UploadCredentials,
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
  create(input: VideoInput, key: string) {
    return this.api.post<CreatedVideo>('/admin/videos', input, {
      headers: { 'Idempotency-Key': key },
    });
  }
  update(id: string, input: VideoInput) {
    return this.api.put<void>('/admin/videos/' + id, input);
  }
  upload(id: string) {
    return this.api.post<UploadCredentials>('/admin/videos/' + id + '/upload');
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
