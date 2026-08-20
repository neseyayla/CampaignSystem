import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './services/auth.service';

/**
 * Keeps the login screen away from someone who is already signed in.
 *
 * Without this, pressing Back after signing in returns to /login while the token is still
 * held, and the shell — which renders whenever there is a token — wraps the login card in a
 * menu and status bar. Bouncing straight to the campaigns list avoids that half-state.
 */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.signedIn() ? router.createUrlTree(['/campaigns/new']) : true;
};
