import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';
import { ConfirmDialogService } from './confirm-dialog.service';

/**
 * The component is rendered directly, and the service is checked against a stubbed MatDialog.
 * The CDK overlay does not attach under jsdom, so driving the real overlay here would be testing
 * the harness rather than the dialog; the Playwright suite covers it in a real browser.
 */
describe('ConfirmDialogComponent', () => {
  let closed: boolean | undefined;

  function render(data: ConfirmDialogData) {
    closed = undefined;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close: (value: boolean) => (closed = value) } },
      ],
    });
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  function button(
    fixture: ReturnType<typeof render>,
    kind: 'confirm' | 'cancel',
  ): HTMLButtonElement {
    const found = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      `.confirm__button--${kind}`,
    );
    if (!found) throw new Error(`no ${kind} button rendered`);
    return found;
  }

  it('closes with true on confirm and false on cancel', () => {
    const fixture = render({ title: 'Τίτλος', message: 'Μήνυμα;' });
    button(fixture, 'confirm').click();
    expect(closed).toBe(true);

    const second = render({ title: 'Τίτλος', message: 'Μήνυμα;' });
    button(second, 'cancel').click();
    expect(closed).toBe(false);
  });

  it('shows the title, the message naming the target, the detail and custom labels', () => {
    const fixture = render({
      title: 'Διαγραφή βίντεο',
      message: 'Να διαγραφεί το «Πλάτη»;',
      detail: 'Δεν επαναφέρεται.',
      confirmLabel: 'Οριστική διαγραφή',
      cancelLabel: 'Άκυρο',
    });

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Διαγραφή βίντεο');
    expect(text).toContain('«Πλάτη»');
    expect(text).toContain('Δεν επαναφέρεται.');
    expect(button(fixture, 'confirm').textContent?.trim()).toBe('Οριστική διαγραφή');
    expect(button(fixture, 'cancel').textContent?.trim()).toBe('Άκυρο');
  });

  it('falls back to Greek default labels', () => {
    const fixture = render({ title: 'Τίτλος', message: 'Μήνυμα;' });
    expect(button(fixture, 'confirm').textContent?.trim()).toBe('Επιβεβαίωση');
    expect(button(fixture, 'cancel').textContent?.trim()).toBe('Ακύρωση');
  });

  it('omits the detail line when there is none', () => {
    const fixture = render({ title: 'Τίτλος', message: 'Μήνυμα;' });
    expect((fixture.nativeElement as HTMLElement).querySelector('.confirm__detail')).toBeNull();
  });

  it('marks a destructive action so it is styled as one', () => {
    const fixture = render({ title: 'Διαγραφή', message: 'Σίγουρα;', destructive: true });
    const root = (fixture.nativeElement as HTMLElement).querySelector('.confirm');
    expect(root?.classList.contains('confirm--destructive')).toBe(true);
  });

  it('answers once and disables both buttons, however many times it is clicked', () => {
    const fixture = render({ title: 'Τίτλος', message: 'Μήνυμα;' });
    const confirm = button(fixture, 'confirm');

    confirm.click();
    expect(closed).toBe(true);

    // A double tap on a phone must not answer a second time with a different value.
    closed = undefined;
    confirm.click();
    button(fixture, 'cancel').click();
    expect(closed).toBeUndefined();

    fixture.detectChanges();
    expect(button(fixture, 'confirm').disabled).toBe(true);
    expect(button(fixture, 'cancel').disabled).toBe(true);
  });
});

describe('ConfirmDialogService', () => {
  @Component({ template: '' })
  class Host {}

  let opened: { component: unknown; config: Record<string, unknown> } | null = null;

  // No default: `configure(undefined)` would otherwise fall back to it, which is the whole
  // case this helper exists to set up.
  function configure(afterClosed: boolean | undefined): void {
    opened = null;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: MatDialog,
          useValue: {
            open: (component: unknown, config: Record<string, unknown>) => {
              opened = { component, config };
              return { afterClosed: () => of(afterClosed) };
            },
          },
        },
      ],
    });
    TestBed.createComponent(Host);
  }

  beforeEach(() => configure(true));

  it('opens the shared component and resolves its answer', async () => {
    const answer = await TestBed.inject(ConfirmDialogService).confirm({
      title: 'Τίτλος',
      message: 'Μήνυμα;',
    });

    expect(answer).toBe(true);
    expect(opened!.component).toBe(ConfirmDialogComponent);
    expect(opened!.config['data']).toEqual({ title: 'Τίτλος', message: 'Μήνυμα;' });
  });

  it('focuses Cancel for a destructive action and Confirm otherwise', async () => {
    const service = TestBed.inject(ConfirmDialogService);

    await service.confirm({ title: 'Τ', message: 'Μ;', destructive: true });
    // Cancel is first in the DOM, so a stray Enter cannot delete anything.
    expect(opened!.config['autoFocus']).toBe('first-tabbable');

    await service.confirm({ title: 'Τ', message: 'Μ;' });
    expect(opened!.config['autoFocus']).toBe('.confirm__button--confirm');
  });

  it('treats a dismissed dialog as a refusal', async () => {
    // Escape and a backdrop click close with undefined, which must never read as "yes".
    configure(undefined);
    expect(await TestBed.inject(ConfirmDialogService).confirm({ title: 'Τ', message: 'Μ;' })).toBe(
      false,
    );
  });

  it('becomes a full-width sheet on a phone and a centred card otherwise', async () => {
    const service = TestBed.inject(ConfirmDialogService);
    // jsdom here has no matchMedia at all, which is also true of some embedded webviews.
    const original = window.matchMedia;
    const matchMedia = vi.fn();
    Object.defineProperty(window, 'matchMedia', { value: matchMedia, configurable: true });

    matchMedia.mockReturnValue({ matches: true } as MediaQueryList);
    await service.confirm({ title: 'Τ', message: 'Μ;' });
    expect(opened!.config['width']).toBe('100vw');
    expect(opened!.config['position']).toEqual({ bottom: '0' });

    matchMedia.mockReturnValue({ matches: false } as MediaQueryList);
    await service.confirm({ title: 'Τ', message: 'Μ;' });
    expect(opened!.config['width']).toBe('440px');
    expect(opened!.config['position']).toBeUndefined();

    Object.defineProperty(window, 'matchMedia', { value: original, configurable: true });
  });

  it('falls back to the desktop layout where matchMedia does not exist', async () => {
    const original = window.matchMedia;
    Object.defineProperty(window, 'matchMedia', { value: undefined, configurable: true });

    await expect(
      TestBed.inject(ConfirmDialogService).confirm({ title: 'Τ', message: 'Μ;' }),
    ).resolves.toBe(true);
    expect(opened!.config['width']).toBe('440px');

    Object.defineProperty(window, 'matchMedia', { value: original, configurable: true });
  });
});
