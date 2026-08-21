import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { authInterceptor } from './auth-interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),

    // Without this HttpClient cannot be injected anywhere in the application. The interceptor
    // attaches the token to our API calls and signs out when the API stops accepting it.
    provideHttpClient(withInterceptors([authInterceptor]))
  ]
};
