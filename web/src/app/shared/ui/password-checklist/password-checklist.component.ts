import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { PASSWORD_RULES } from '../../forms/password-rules';

/**
 * Live checklist of the password rules, ticking as the user types. This is how the rules are
 * communicated — up front, not as an error after the fact.
 */
@Component({
  selector: 'app-password-checklist',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="checklist" aria-label="Κανόνες κωδικού">
      @for (rule of rules; track rule.key) {
        <li class="checklist__item" [class.checklist__item--met]="met()[rule.key]">
          <mat-icon class="checklist__icon" aria-hidden="true">
            {{ met()[rule.key] ? 'check_circle' : 'radio_button_unchecked' }}
          </mat-icon>
          <span>{{ rule.label }}</span>
          <span class="checklist__sr-only">{{ met()[rule.key] ? ', πληρείται' : ', δεν πληρείται' }}</span>
        </li>
      }
    </ul>
  `,
  styles: `
    .checklist {
      list-style: none;
      margin: 0 0 8px;
      padding: 0 16px;
      display: grid;
      gap: 4px;
      font: var(--mat-sys-body-small);
      color: var(--brand-muted);
    }

    .checklist__item {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .checklist__item--met {
      color: var(--brand-text);
    }

    .checklist__icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }

    .checklist__item--met .checklist__icon {
      color: var(--brand-accent);
    }

    // Read by screen readers, invisible otherwise (the CDK helper class is not loaded globally).
    .checklist__sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
})
export class PasswordChecklistComponent {
  readonly password = input('');

  protected readonly rules = PASSWORD_RULES;

  protected readonly met = computed<Record<string, boolean>>(() => {
    const value = this.password() ?? '';
    return Object.fromEntries(PASSWORD_RULES.map((rule) => [rule.key, rule.test(value)]));
  });
}
