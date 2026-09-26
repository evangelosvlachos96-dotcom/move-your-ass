import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { VideoLibraryComponent } from './library.component';
import { VideosApi } from '../../core/videos/videos.api';
describe('Video library', () => {
  const params = new BehaviorSubject(convertToParamMap({}));
  const api = { tags: vi.fn(), list: vi.fn() };
  beforeEach(() => {
    params.next(convertToParamMap({}));
    api.tags.mockReset().mockReturnValue(of([]));
    api.list.mockReset().mockReturnValue(of({ items: [], totalCount: 0, page: 1, totalPages: 0 }));
    TestBed.configureTestingModule({
      imports: [VideoLibraryComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
        { provide: VideosApi, useValue: api },
      ],
    });
  });
  it('keeps equipment unset on first load and forwards selected tag filters', async () => {
    const fixture = TestBed.createComponent(VideoLibraryComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(api.list.mock.calls[0][0]).not.toHaveProperty('equipment');
    params.next(
      convertToParamMap({ equipment: 'false', audience: 'Female', tags: ['tag-a', 'tag-b'] }),
    );
    fixture.detectChanges();
    expect(api.list.mock.lastCall?.[0]).toMatchObject({
      equipment: 'false',
      audience: 'Female',
      'tags[0]': 'tag-a',
      'tags[1]': 'tag-b',
    });
  });
  it('can retry an error without changing the route', async () => {
    api.list.mockReturnValueOnce(throwError(() => new Error('offline')));
    const fixture = TestBed.createComponent(VideoLibraryComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    buttons.find((b) => b.textContent?.includes('Επανάληψη'))!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(api.list).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('Δεν υπάρχουν προπονήσεις');
  });
});
