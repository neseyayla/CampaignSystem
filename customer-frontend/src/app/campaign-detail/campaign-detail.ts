import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';

import { Card, CustomerCampaignDetail } from '../models/customer-campaign';
import { CustomerService } from '../services/customer.service';
import { TopBar } from '../shared/top-bar';

/**
 * One campaign in full: its terms, the customer's progress, and — for an enrollment
 * campaign they have not joined — the sign-up. The list shows a compact card; everything
 * that does not fit there lives here.
 */
@Component({
  selector: 'app-campaign-detail',
  imports: [RouterLink, TopBar],
  templateUrl: './campaign-detail.html',
  styleUrl: './campaign-detail.css'
})
export class CampaignDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CustomerService);

  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly detail = signal<CustomerCampaignDetail | null>(null);

  readonly choosingCard = signal(false);
  readonly cards = signal<{ card: Card; productName: string }[]>([]);
  readonly enrolling = signal(false);
  readonly enrollError = signal<string | null>(null);

  readonly coverClass = computed(() => `c${(this.detail()?.campaign.campaignId ?? 0) % 5}`);

  constructor() {
    this.route.paramMap
      .pipe(switchMap(params => this.service.campaign(Number(params.get('id')))))
      .subscribe({
        next: detail => {
          this.detail.set(detail);
          this.loading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          // 404 covers "does not exist", "no longer open" and "not eligible" alike — from
          // the customer's side those are the same answer.
          this.notFound.set(err.status === 404);
          this.loading.set(false);
        }
      });
  }

  /**
   * The campaign's terms, written once by an operator from its rules and shown here
   * exactly as reviewed — not re-derived on this screen.
   */
  terms(): string[] {
    return this.detail()?.campaign.conditions ?? [];
  }

  qualifyingCount(): number {
    return this.detail()?.progress.lines.reduce((t, l) => t + l.qualifyingCount, 0) ?? 0;
  }

  capped(): boolean {
    return this.detail()?.progress.lines.some(l => l.capApplied) ?? false;
  }

  capPercent(): number {
    const d = this.detail();
    const cap = d?.campaign.maxRewardAmount;

    if (!d || cap === null || cap === undefined || cap <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((d.progress.totalRewardPoint / cap) * 100));
  }

  daysLeft(): number {
    const end = this.detail()?.campaign.endDate;
    return end ? Math.ceil((new Date(end).getTime() - Date.now()) / 86_400_000) : 0;
  }

  join(): void {
    const c = this.detail()?.campaign;

    if (!c) {
      return;
    }

    // A card based campaign accumulates on one card, so it has to be told which. A customer
    // based one pools every card and must not name one.
    if (c.earningType === 'CardBased') {
      this.openCardPicker();
      return;
    }

    this.send(null);
  }

  chooseCard(cardId: number): void {
    this.send(cardId);
  }

  cancelCardPicker(): void {
    this.choosingCard.set(false);
    this.enrollError.set(null);
  }

  points(value: number): string {
    return value.toLocaleString('tr-TR', { maximumFractionDigits: 0 });
  }

  day(value: string): string {
    return new Date(value).toLocaleDateString('tr-TR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    });
  }

  private openCardPicker(): void {
    this.enrollError.set(null);
    this.choosingCard.set(true);

    if (this.cards().length > 0) {
      return;
    }

    this.service.cards().subscribe({
      next: cards => this.cards.set(cards),
      error: () => this.enrollError.set('Kartlarınız alınamadı.')
    });
  }

  private send(cardId: number | null): void {
    const id = this.detail()?.campaign.campaignId;

    if (id === undefined) {
      return;
    }

    this.enrolling.set(true);
    this.enrollError.set(null);

    this.service.enroll(id, cardId).subscribe({
      next: () => {
        this.enrolling.set(false);
        this.choosingCard.set(false);

        // Reload the one campaign so its enrolled flag and progress come back fresh.
        this.service.campaign(id).subscribe(detail => this.detail.set(detail));
      },
      error: (err: HttpErrorResponse) => {
        this.enrolling.set(false);
        this.enrollError.set(this.messageOf(err));
      }
    });
  }

  private messageOf(err: HttpErrorResponse): string {
    const body: unknown = err.error;

    if (typeof body === 'string' && body.trim().length > 0) {
      return body;
    }

    return 'Kampanyaya katılınamadı.';
  }
}
