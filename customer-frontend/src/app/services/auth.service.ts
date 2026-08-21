import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { API_BASE_URL } from '../api-config';

interface LoginResult {
  token: string;
  expiresAt: string;
  customerNumber: string;
}

const TOKEN_KEY = 'campaign.token';
const NUMBER_KEY = 'campaign.customerNumber';
const EXPIRY_KEY = 'campaign.expiresAt';

/**
 * Who is signed in, and the token that proves it.
 *
 * The token lives in sessionStorage, which is worth being plain about: any script running on
 * this page can read it, so a cross-site scripting hole would hand it over. An httpOnly
 * cookie would put it out of reach of scripts, at the cost of carrying CSRF protection and
 * credentialed CORS. This is the pragmatic choice for the project, not the safest one.
 *
 * Closing the tab ends the session, which sessionStorage gives for free.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _token = signal<string | null>(sessionStorage.getItem(TOKEN_KEY));
  private readonly _customerNumber = signal<string | null>(sessionStorage.getItem(NUMBER_KEY));

  readonly token = this._token.asReadonly();
  readonly customerNumber = this._customerNumber.asReadonly();
  readonly signedIn = computed(() => this._token() !== null);

  constructor() {
    // A token that has already run out is worse than none: it would let the screen render
    // and then fail every request with a 401.
    const expiry = sessionStorage.getItem(EXPIRY_KEY);

    if (expiry !== null && new Date(expiry).getTime() <= Date.now()) {
      this.signOut();
    }
  }

  login(customerNumber: string, password: string): Observable<LoginResult> {
    return this.http
      .post<LoginResult>(`${API_BASE_URL}/auth/login`, { customerNumber, password })
      .pipe(tap(result => this.store(result)));
  }

  signOut(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(NUMBER_KEY);
    sessionStorage.removeItem(EXPIRY_KEY);

    this._token.set(null);
    this._customerNumber.set(null);
  }

  private store(result: LoginResult): void {
    sessionStorage.setItem(TOKEN_KEY, result.token);
    sessionStorage.setItem(NUMBER_KEY, result.customerNumber);
    sessionStorage.setItem(EXPIRY_KEY, result.expiresAt);

    this._token.set(result.token);
    this._customerNumber.set(result.customerNumber);
  }
}
