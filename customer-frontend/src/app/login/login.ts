import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * Signing in, laid out like a retail bank's internet-banking entrance.
 *
 * A note on what this is: a demo of that flow, on localhost, under the developer's own name.
 * It borrows the shape of a bank login — the individual/commercial toggle, the confirmation
 * code, the security notices — but it is not, and must not be dressed up as, the real thing.
 *
 * The confirmation code is a client-side check only. It keeps a casual script from hammering
 * the sign-in form, and it makes the screen feel complete, but it is not security: a real one
 * is issued and verified by the server.
 *
 * Whatever the API says about a failed sign-in is shown as it stands, and it deliberately
 * says the same thing for a wrong number as for a wrong password.
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly segment = signal<'individual' | 'commercial'>('individual');

  readonly customerNumber = signal('');
  readonly password = signal('');
  readonly confirmation = signal('');

  /** The code the customer has to copy back, redrawn on demand and after a wrong attempt. */
  readonly captcha = signal(this.newCaptcha());

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  refreshCaptcha(): void {
    this.captcha.set(this.newCaptcha());
    this.confirmation.set('');
  }

  submit(): void {
    const number = this.customerNumber().trim();
    const password = this.password();

    if (number.length === 0 || password.length === 0) {
      this.error.set('Müşteri numarası ve şifre gerekli.');
      return;
    }

    // Case-insensitive: the code is shown in mixed case only to make it awkward for a script,
    // not to trip up someone typing it by hand.
    if (this.confirmation().trim().toLowerCase() !== this.captcha().toLowerCase()) {
      this.error.set('Onay kodu hatalı.');
      this.refreshCaptcha();
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.auth.login(number, password).subscribe({
      next: () => {
        // Cleared before leaving, so the value does not sit in memory behind the next screen.
        this.password.set('');
        void this.router.navigate(['/kampanyalar']);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(this.messageOf(err));
        this.refreshCaptcha();
      }
    });
  }

  /** The API answers a refused sign-in with plain text, which HttpClient hands back raw. */
  private messageOf(err: HttpErrorResponse): string {
    const body: unknown = err.error;

    if (typeof body === 'string' && body.trim().length > 0) {
      return body;
    }

    return err.status === 0
      ? 'Sunucuya ulaşılamadı. API çalışıyor mu?'
      : 'Giriş yapılamadı.';
  }

  /** Five characters, dropping the ones that read as one another (0/O, 1/I/l). */
  private newCaptcha(): string {
    const alphabet = 'abcdefghjkmnpqrstuvwxyz23456789';
    let code = '';

    for (let i = 0; i < 5; i++) {
      const pick = Math.floor(Math.random() * alphabet.length);
      const ch = alphabet[pick];
      code += i % 2 === 0 ? ch.toUpperCase() : ch;
    }

    return code;
  }
}
