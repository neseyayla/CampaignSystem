import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';

export interface CampaignReport {
  campaignId: number;
  campaignName: string;
  customerCount: number;
  totalPointsEarned: number;
  totalPointsClawedBack: number;
  totalRefundAmount: number;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/reports`;

  /** Campaign statistics looked up by campaign id. 404 when no campaign has that id. */
  getById(campaignId: number): Observable<CampaignReport> {
    return this.http.get<CampaignReport>(`${this.baseUrl}/campaign/${campaignId}`);
  }
}
