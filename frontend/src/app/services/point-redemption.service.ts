import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import { CreatePointRedemption, PointRedemption } from '../models/campaign';

/** Everything the application knows about talking to the point-redemption endpoints. */
@Injectable({ providedIn: 'root' })
export class PointRedemptionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/campaigns`;

  getByCampaign(campaignId: number): Observable<PointRedemption[]> {
    return this.http.get<PointRedemption[]>(`${this.baseUrl}/${campaignId}/redemptions`);
  }

  create(campaignId: number, redemption: CreatePointRedemption): Observable<PointRedemption> {
    return this.http.post<PointRedemption>(`${this.baseUrl}/${campaignId}/redemptions`, redemption);
  }
}
