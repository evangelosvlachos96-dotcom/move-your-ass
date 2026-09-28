import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../http/api-client.service';
import { About, AboutInput, ContactInput, PhotoTicket } from './site.models';

@Injectable({ providedIn: 'root' })
export class SiteApi {
  private readonly api = inject(ApiClient);

  about() {
    return this.api.get<About>('/site/about');
  }

  save(input: AboutInput) {
    return this.api.put<void>('/admin/site/about', input);
  }

  contact(input: ContactInput) {
    return this.api.post<void>('/site/contact', input);
  }

  photoTicket(contentType: string, sizeBytes: number) {
    return this.api.post<PhotoTicket>('/admin/site/about/photo', { contentType, sizeBytes });
  }

  confirmPhoto(objectKey: string) {
    return this.api.put<void>('/admin/site/about/photo', { objectKey });
  }

  removePhoto() {
    return this.api.delete<void>('/admin/site/about/photo');
  }

  sendTestEmail() {
    return this.api.post<void>('/admin/site/test-email');
  }
}
