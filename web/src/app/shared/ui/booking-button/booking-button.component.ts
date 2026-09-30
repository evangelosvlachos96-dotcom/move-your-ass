import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * "Κλείσε ραντεβού" — the one call to action that leaves the app.
 *
 * Defined once so the four places it appears cannot drift: the sidebar, the phone bottom bar,
 * the top of the client library and the end of the trainer's page. It is orange rather than lime
 * on purpose — the lime accent means "the primary action on this screen", and booking is never
 * that; it is the one thing that takes you somewhere else.
 *
 * `rel="noopener noreferrer"` is not decoration: the target is a third-party scheduling page the
 * trainer chose, and without `noopener` it gets a handle on the window it was opened from.
 */
@Component({
  selector: 'app-booking-button',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a
      class="booking"
      [class.booking--block]="block()"
      [href]="url()"
      target="_blank"
      rel="noopener noreferrer"
    >
      <mat-icon class="booking__icon" aria-hidden="true">event_available</mat-icon>
      <span>{{ label() }}</span>
    </a>
  `,
  styleUrl: './booking-button.component.scss',
})
export class BookingButtonComponent {
  /** Always an https address: the server validates it and drops anything else on the way out. */
  readonly url = input.required<string>();
  readonly label = input('Κλείσε ραντεβού');
  /** Full width, for the sidebar and the end of the About page. */
  readonly block = input(false);
}
