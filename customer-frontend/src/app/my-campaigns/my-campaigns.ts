import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, HostListener, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';

import {
  Card,
  CustomerCampaign,
  CustomerCampaignDetail,
  RewardSummary
} from '../models/customer-campaign';
import { RouterLink } from '@angular/router';

import { TopBar } from '../shared/top-bar';
import { CustomerService } from '../services/customer.service';

/** Which set of campaigns the grid is showing. */
export type Tab = 'all' | 'joinable' | 'earning' | 'earned';

/**
 * The whole customer screen: what is on offer, what is counting for them now, and what they
 * have already been paid.
 *
 * The campaigns arrive in one list and are split here rather than in separate calls, because
 * the split is presentation. A mass campaign and one the customer signed up for are both
 * simply counting; one they have not signed up for is the only kind with a button.
 */
@Component({
  selector: 'app-my-campaigns',
  imports: [TopBar, RouterLink],
  templateUrl: './my-campaigns.html',
  styleUrls: ['./my-campaigns.css', './my-campaigns-header.scss']
})
export class MyCampaigns {
  private readonly service = inject(CustomerService);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly details = signal<CustomerCampaignDetail[]>([]);
  readonly rewards = signal<RewardSummary | null>(null);

  readonly tab = signal<Tab>('all');

  /** Already counting: every mass campaign, plus the ones the customer joined. */
  readonly earning = computed(() =>
    this.details().filter(d => !d.campaign.enrollmentRequired || d.campaign.enrolled)
  );

  /** Open to the customer but not joined yet — the only ones with anything to press. */
  readonly joinable = computed(() =>
    this.details().filter(d => d.campaign.enrollmentRequired && !d.campaign.enrolled)
  );

  /** How the grid is ordered, and whether the sort menu is open. */
  readonly sortBy = signal<'end' | 'reward' | 'name'>('end');
  readonly sortOpen = signal(false);

  /** What the grid shows under the tab in force, in the chosen order. */
  readonly visible = computed(() => {
    const list = (() => {
      switch (this.tab()) {
        case 'joinable':
          return this.joinable();
        case 'earning':
          return this.earning();
        default:
          return this.details();
      }
    })();

    const by = this.sortBy();

    // A copy, because a computed must not mutate the array a signal handed it.
    return [...list].sort((a, b) => {
      switch (by) {
        case 'reward':
          return (b.campaign.rewardPoint ?? 0) - (a.campaign.rewardPoint ?? 0);
        case 'name':
          return a.campaign.name.localeCompare(b.campaign.name, 'tr');
        default:
          return +new Date(a.campaign.endDate) - +new Date(b.campaign.endDate);
      }
    });
  });

  /** The campaign whose card picker is open, if any. */
  readonly choosingCardFor = signal<number | null>(null);
  readonly cards = signal<{ card: Card; productName: string }[]>([]);
  readonly enrolling = signal(false);
  readonly enrollError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.enrollError.set(null);
    this.choosingCardFor.set(null);

    // The cards are not fetched here: they are only needed if the customer opens a card
    // picker, and most visits never do.
    forkJoin({
      details: this.service.campaignsWithProgress(),
      rewards: this.service.rewards()
    }).subscribe({
      next: ({ details, rewards }) => {
        this.details.set(details);
        this.rewards.set(rewards);
        this.loading.set(false);
      },
      error: () => {
        this.details.set([]);
        this.rewards.set(null);
        // A 401 is already handled by the interceptor, which signs the customer out.
        this.error.set('Bilgiler alınamadı. API çalışıyor mu?');
        this.loading.set(false);
      }
    });
  }

  join(detail: CustomerCampaignDetail): void {
    const campaignId = detail.campaign.campaignId;

    // A customer based campaign pools every card and must not name one.
    if (detail.campaign.earningType !== 'CardBased') {
      this.send(campaignId, null);
      return;
    }

    // A card based campaign accrues on a single card, so we need to know which. With the cards
    // already in hand we can decide right away; otherwise fetch them first.
    if (this.cards().length > 0) {
      this.enrollOrPick(campaignId);
      return;
    }

    this.enrollError.set(null);
    this.enrolling.set(true);
    this.service.cards().subscribe({
      next: cards => {
        this.cards.set(cards);
        this.enrolling.set(false);
        this.enrollOrPick(campaignId);
      },
      error: () => {
        this.enrolling.set(false);
        this.openCardPicker(campaignId);   // open so the error is visible
        this.enrollError.set('Kartlarınız alınamadı.');
      }
    });
  }

  /**
   * One card means there is nothing to choose, so join with it directly; more than one opens
   * the picker so the customer decides which card the campaign should accrue on.
   */
  private enrollOrPick(campaignId: number): void {
    const cards = this.cards();

    if (cards.length === 1) {
      this.send(campaignId, cards[0].card.id);
    } else {
      this.openCardPicker(campaignId);
    }
  }

  chooseCard(cardId: number): void {
    const campaignId = this.choosingCardFor();

    if (campaignId !== null) {
      this.send(campaignId, cardId);
    }
  }

  cancelCardPicker(): void {
    this.choosingCardFor.set(null);
    this.enrollError.set(null);
  }

  /** True when this campaign is one the customer is already earning from. */
  isEarning(detail: CustomerCampaignDetail): boolean {
    return !detail.campaign.enrollmentRequired || detail.campaign.enrolled;
  }

  /** The campaign's conditions, in the words a card holder would use. */
  terms(campaign: CustomerCampaign): string[] {
    const list: string[] = [];

    if (campaign.minimumAmount !== null) {
      list.push(`En az ${this.points(campaign.minimumAmount)} TL harcama`);
    }

    if (campaign.maximumAmount !== null) {
      list.push(`En çok ${this.points(campaign.maximumAmount)} TL harcama`);
    }

    if (campaign.merchants.length > 0) {
      list.push(campaign.merchants.join(', '));
    }

    if (campaign.transactionCodes.length > 0) {
      list.push(campaign.transactionCodes.join(', '));
    }

    return list;
  }

  /** How many transactions have counted so far, across every card. */
  qualifyingCount(detail: CustomerCampaignDetail): number {
    return detail.progress.lines.reduce((total, line) => total + line.qualifyingCount, 0);
  }

  /** True when the campaign's ceiling is already holding the reward down. */
  capped(detail: CustomerCampaignDetail): boolean {
    return detail.progress.lines.some(line => line.capApplied);
  }

  /**
   * How far the customer has come towards the campaign's ceiling, as a percentage.
   * Zero rather than null, since the caller only draws a bar when a ceiling exists.
   */
  capPercent(detail: CustomerCampaignDetail): number {
    const cap = detail.campaign.maxRewardAmount;

    if (cap === null || cap <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((detail.progress.totalRewardPoint / cap) * 100));
  }

  /** Whole days until the campaign closes. */
  daysLeft(campaign: CustomerCampaign): number {
    return Math.ceil((new Date(campaign.endDate).getTime() - Date.now()) / 86_400_000);
  }

  /**
   * One of a handful of cover treatments, chosen from the campaign's id.
   *
   * The bank's own pages put a photograph here. There is none to serve, so the space carries
   * the campaign's reward instead — which is the thing a card holder is looking for anyway.
   * Deriving the colour from the id keeps a campaign looking the same on every visit.
   */
  coverClass(campaign: CustomerCampaign): string {
    return `c${campaign.campaignId % 5}`;
  }

  setSort(by: 'end' | 'reward' | 'name'): void {
    this.sortBy.set(by);
    this.sortOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.sortOpen() && !this.host.nativeElement.contains(event.target as Node)) {
      this.sortOpen.set(false);
    }
  }

  points(value: number): string {
    return value.toLocaleString('tr-TR', { maximumFractionDigits: 0 });
  }

  /** "31 Ekim" — the form the last-day line uses. */
  dayShort(value: string): string {
    return new Date(value).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long' });
  }

  /** "31 Ekim 2026" — the form the full end-date line uses. */
  day(value: string): string {
    return new Date(value).toLocaleDateString('tr-TR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    });
  }

  private openCardPicker(campaignId: number): void {
    this.enrollError.set(null);
    this.choosingCardFor.set(campaignId);

    if (this.cards().length > 0) {
      return;
    }

    this.service.cards().subscribe({
      next: cards => this.cards.set(cards),
      error: () => this.enrollError.set('Kartlarınız alınamadı.')
    });
  }

  private send(campaignId: number, cardId: number | null): void {
    this.enrolling.set(true);
    this.enrollError.set(null);

    this.service.enroll(campaignId, cardId).subscribe({
      next: () => {
        this.enrolling.set(false);
        this.choosingCardFor.set(null);

        // Reloading rather than patching the one campaign: joining changes which tab it
        // belongs to, and the progress figure is now worth asking for.
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.enrolling.set(false);
        this.enrollError.set(this.messageOf(err));

        // A direct single-card join failed with the picker closed, so open it — otherwise the
        // error has nowhere to show and the customer cannot retry with another card.
        if (cardId !== null && this.choosingCardFor() === null) {
          this.choosingCardFor.set(campaignId);
        }
      }
    });
  }

  /**
   * A rejected enrolment comes back as plain text, which HttpClient cannot parse as JSON and
   * hands over raw. Those messages say something useful — which card, why not — so they are
   * shown as they are. Anything else is reported in general terms, since a status code is
   * nothing the customer can act on.
   */
  private messageOf(err: HttpErrorResponse): string {
    const body: unknown = err.error;

    if (typeof body === 'string' && body.trim().length > 0) {
      return body;
    }

    if (body && typeof body === 'object' && 'text' in body && typeof body.text === 'string') {
      return body.text;
    }

    return 'Kampanyaya katılınamadı.';
  }
}
