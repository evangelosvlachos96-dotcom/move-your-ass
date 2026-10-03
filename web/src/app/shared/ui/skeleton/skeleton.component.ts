import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Content-shaped placeholders share one motion, palette and accessible loading announcement. */
@Component({
  selector: 'app-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div role="status" aria-live="polite" aria-busy="true" [attr.aria-label]="label()">
    <span class="visually-hidden">{{ label() }}</span>
    <div aria-hidden="true" class="skeleton" [class.cards]="variant() === 'cards'">
      @for (item of items; track item) {
        @if (item < count()) {
          <div class="unit" [class.row]="variant() === 'rows'" [class.card]="variant() === 'cards'">
            @if (variant() === 'rows' || variant() === 'profile') {
              <span class="bone avatar"></span>
            }
            <div class="copy">
              @if (variant() === 'cards' || variant() === 'player') {
                <span class="bone media"></span>
              }
              <span class="bone line title"></span><span class="bone line"></span>
              @if (variant() === 'form') {
                <span class="bone field"></span><span class="bone field"></span>
              }
              <span class="bone line short"></span>
            </div>
          </div>
        }
      }
    </div>
  </div>`,
  styleUrl: './skeleton.component.scss',
})
export class SkeletonComponent {
  readonly variant = input<'rows' | 'cards' | 'player' | 'form' | 'profile' | 'text'>('text');
  readonly count = input(1);
  readonly label = input('Φόρτωση περιεχομένου…');
  protected readonly items = [0, 1, 2, 3, 4, 5];
}
