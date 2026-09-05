import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { API_BASE_URL } from './api-config';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Shown in the status bar so it is obvious which API the screen is talking to. */
  protected readonly apiBaseUrl = API_BASE_URL;

  /** Drives the chrome: signed out, only the login screen shows — no menu, no status bar. */
  protected readonly signedIn = this.auth.signedIn;

  /** Whether the side menu is expanded. Collapsing it hands the whole width to the content. */
  protected readonly menuOpen = signal(true);

  protected toggleMenu(): void {
    this.menuOpen.update(open => !open);
  }

  protected signOut(): void {
    this.auth.signOut();
    void this.router.navigate(['/login']);
  }
}
