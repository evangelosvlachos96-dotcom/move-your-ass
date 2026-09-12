import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthStore } from '../../../core/auth/auth.store';
import { NotifyService } from '../../../core/ui/notify.service';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ChevronLoaderComponent } from '../../../shared/ui/chevron-loader/chevron-loader.component';

const NAME_MAX_LENGTH = 80;

/** First and last name only. The email is shown but cannot be changed here. */
@Component({
  selector: 'app-profile',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    ChevronLoaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  private readonly auth = inject(AuthService);
  private readonly store = inject(AuthStore);
  private readonly notify = inject(NotifyService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly submitting = signal(false);
  protected readonly nameMaxLength = NAME_MAX_LENGTH;

  protected readonly form = new FormGroup({
    firstName: new FormControl(this.store.user()?.firstName ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)],
    }),
    lastName: new FormControl(this.store.user()?.lastName ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)],
    }),
    email: new FormControl({ value: this.store.user()?.email ?? '', disabled: true }, { nonNullable: true }),
  });

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      focusFirstInvalid(this.form, this.host.nativeElement);
      return;
    }

    const { firstName, lastName } = this.form.getRawValue();

    this.submitting.set(true);
    this.auth
      .updateProfile({ firstName: firstName.trim(), lastName: lastName.trim() })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.form.markAsPristine();
          this.notify.info('Το προφίλ σου ενημερώθηκε.');
        },
        // The error interceptor has already told the user what went wrong.
        error: () => undefined,
      });
  }
}
