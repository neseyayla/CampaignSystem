import { Component, ElementRef, HostListener, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * The bar every signed-in screen shares: the mark on the left, and on the right the customer's
 * own menu — profile and sign-out — behind an avatar.
 *
 * Its own component so the campaigns screen and the profile screen carry the identical bar
 * rather than each drawing their own and drifting apart.
 */
@Component({
  selector: 'app-top-bar',
  imports: [],
  templateUrl: './top-bar.html',
  styleUrl: './top-bar.css'
})
export class TopBar {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly customerNumber = this.auth.customerNumber;
  readonly open = signal(false);

  toggle(): void {
    this.open.update(v => !v);
  }

  goProfile(): void {
    this.open.set(false);
    void this.router.navigate(['/profil']);
  }

  goCampaigns(): void {
    this.open.set(false);
    void this.router.navigate(['/kampanyalar']);
  }

  signOut(): void {
    this.open.set(false);
    this.auth.signOut();
    void this.router.navigate(['/login']);
  }

  /** Closes the menu on any click that lands outside this component. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }
}
