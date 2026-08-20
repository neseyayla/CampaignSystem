import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AuthService } from './services/auth.service';

/**
 * Attaches the token to every call to our own API, and signs the user out the moment the API
 * stops accepting it.
 *
 * Signing out on a 401 matters because tokens expire on their own: without this the screen
 * would sit there looking signed in while every request quietly failed.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.token();

  // Only our own API. A token must never be attached to a third party's address.
  const authorised =
    token !== null && request.url.startsWith(API_BASE_URL)
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorised).pipe(
    catchError((error: HttpErrorResponse) => {
      // Not on the sign-in call itself: a wrong password is a message to read, not a reason to
      // throw the user back to a screen they are already on.
      if (error.status === 401 && !request.url.endsWith('/auth/admin/login')) {
        auth.signOut();
        void router.navigate(['/login']);
      }

      return throwError(() => error);
    })
  );
};
