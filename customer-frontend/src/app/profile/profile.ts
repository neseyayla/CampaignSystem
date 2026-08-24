import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';

import { Card, CustomerProfile } from '../models/customer-campaign';
import { CustomerService } from '../services/customer.service';
import { TopBar } from '../shared/top-bar';

/**
 * The customer's own settings: who they are, the cards they hold, and a form to change their
 * password. Reached from the account menu in the bar, never by editing a URL — the API answers
 * only about whoever the token names.
 */
@Component({
  selector: 'app-profile',
  imports: [FormsModule, TopBar],
  templateUrl: './profile.html',
  styleUrl: './profile.css'
})
export class Profile {
  private readonly service = inject(CustomerService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly profile = signal<CustomerProfile | null>(null);
  readonly cards = signal<{ card: Card; productName: string }[]>([]);

  readonly genderLabel = computed(() => {
    const g = this.profile()?.gender;
    return g === 'Female' ? 'Kadın' : g === 'Male' ? 'Erkek' : '—';
  });

  // Password form.
  /** The change-password form is hidden until the customer opens it. */
  readonly pwOpen = signal(false);

  readonly current = signal('');
  readonly next = signal('');
  readonly confirm = signal('');
  readonly saving = signal(false);
  readonly pwError = signal<string | null>(null);
  readonly pwDone = signal(false);

  constructor() {
    forkJoin({
      profile: this.service.profile(),
      cards: this.service.cards()
    }).subscribe({
      next: ({ profile, cards }) => {
        this.profile.set(profile);
        this.cards.set(cards);
        this.loading.set(false);
      },
      error: () => {
        // A 401 is already handled by the interceptor, which signs the customer out.
        this.error.set('Bilgiler alınamadı.');
        this.loading.set(false);
      }
    });
  }

  changePassword(): void {
    this.pwError.set(null);
    this.pwDone.set(false);

    if (this.current().length === 0 || this.next().length === 0) {
      this.pwError.set('Mevcut ve yeni şifre gerekli.');
      return;
    }

    if (this.next().length < 6) {
      this.pwError.set('Yeni şifre en az 6 karakter olmalı.');
      return;
    }

    if (this.next() !== this.confirm()) {
      this.pwError.set('Yeni şifreler eşleşmiyor.');
      return;
    }

    this.saving.set(true);

    this.service.changePassword(this.current(), this.next()).subscribe({
      next: () => {
        this.saving.set(false);
        this.pwDone.set(true);
        this.current.set('');
        this.next.set('');
        this.confirm.set('');
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.pwError.set(this.messageOf(err));
      }
    });
  }

  private messageOf(err: HttpErrorResponse): string {
    const body: unknown = err.error;

    if (typeof body === 'string' && body.trim().length > 0) {
      return body;
    }

    return 'Şifre değiştirilemedi.';
  }
}
