import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './services/auth.service';

/** Keeps the staff screens behind a sign-in. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.signedIn() ? true : router.createUrlTree(['/login']);
};
