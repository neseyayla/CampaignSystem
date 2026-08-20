import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * The staff sign-in. The only screen reachable without a token; everything else sits behind
 * the guard.
 *
 * A failed sign-in shows the one message the API returns, which is the same for a wrong
 * password, an unknown number and a customer who is not an admin — the screen deliberately
 * does not try to be more specific than the API is.
 */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    customerNumber: ['', Validators.required],
    password: ['', Validators.required]
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const { customerNumber, password } = this.form.getRawValue();

    this.auth.login(customerNumber, password).subscribe({
      next: () => void this.router.navigate(['/campaigns/new']),
      error: (response: HttpErrorResponse) => {
        this.error.set(
          typeof response.error === 'string' && response.error.length > 0
            ? response.error
            : 'Giriş yapılamadı. Lütfen tekrar deneyin.'
        );
        this.submitting.set(false);
      }
    });
  }
}
