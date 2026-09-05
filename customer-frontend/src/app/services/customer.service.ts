import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, of, switchMap } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import {
  Card,
  CustomerCampaign,
  CustomerCampaignDetail,
  CustomerProfile,
  CustomerTransaction,
  Product,
  RewardBreakdown,
  RewardSummary
} from '../models/customer-campaign';

/**
 * Everything this screen asks the API for.
 *
 * No call names a customer. The API reads who is asking from the token the interceptor
 * attaches, so there is no id to pass here and none to get wrong.
 */
@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);

  /** The running and upcoming campaigns this customer could earn from. */
  campaigns(): Observable<CustomerCampaign[]> {
    return this.http.get<CustomerCampaign[]>(`${API_BASE_URL}/me/campaigns`);
  }

  /** One campaign together with the customer's standing in it. */
  campaign(campaignId: number): Observable<CustomerCampaignDetail> {
    return this.http.get<CustomerCampaignDetail>(`${API_BASE_URL}/me/campaigns/${campaignId}`);
  }

  /**
   * Every campaign with its progress filled in.
   *
   * One request per campaign, because the list endpoint returns the terms and the detail
   * endpoint returns the standing. A handful of open campaigns makes that unremarkable; if
   * the catalogue ever grows, the fix is a list endpoint that carries the progress, not a
   * loop that runs longer.
   */
  campaignsWithProgress(): Observable<CustomerCampaignDetail[]> {
    return this.campaigns().pipe(
      switchMap(campaigns =>
        campaigns.length === 0
          ? of([])
          : forkJoin(campaigns.map(c => this.campaign(c.campaignId)))
      )
    );
  }

  /** What the customer has already been paid, campaign by campaign. */
  rewards(): Observable<RewardSummary> {
    return this.http.get<RewardSummary>(`${API_BASE_URL}/me/rewards`);
  }

  /** The purchases behind one finished campaign's reward — earners and refunds. */
  rewardBreakdown(campaignId: number): Observable<RewardBreakdown> {
    return this.http.get<RewardBreakdown>(`${API_BASE_URL}/me/rewards/${campaignId}/transactions`);
  }

  /**
   * The customer's cards, with the product names attached.
   *
   * Two calls rather than one: the cards carry the product only by id, and a card reads as
   * "Visa Gold" to its holder, never as "product 3". The product list is reference data the
   * whole bank shares, so it is not behind the customer's token.
   */
  cards(): Observable<{ card: Card; productName: string }[]> {
    return forkJoin({
      cards: this.http.get<Card[]>(`${API_BASE_URL}/me/cards`),
      products: this.http.get<Product[]>(`${API_BASE_URL}/products`)
    }).pipe(
      switchMap(({ cards, products }) => {
        const nameOf = new Map(products.map(p => [p.id, p.productName]));

        return of(
          cards.map(card => ({
            card,
            productName: nameOf.get(card.productId) ?? `Ürün ${card.productId}`
          }))
        );
      })
    );
  }

  /** Signs the customer up. cardId is required for a card based campaign only. */
  enroll(campaignId: number, cardId: number | null): Observable<unknown> {
    return this.http.post(`${API_BASE_URL}/me/campaigns/${campaignId}/enrollment`, { cardId });
  }

  /** The signed-in customer's own details. */
  profile(): Observable<CustomerProfile> {
    return this.http.get<CustomerProfile>(`${API_BASE_URL}/me/profile`);
  }

  /** The customer's own spending history, newest first. */
  transactions(): Observable<CustomerTransaction[]> {
    return this.http.get<CustomerTransaction[]>(`${API_BASE_URL}/me/transactions`);
  }

  /** Changes the customer's own password; the current one is verified server-side. */
  changePassword(currentPassword: string, newPassword: string): Observable<unknown> {
    return this.http.put(`${API_BASE_URL}/me/password`, { currentPassword, newPassword });
  }
}
