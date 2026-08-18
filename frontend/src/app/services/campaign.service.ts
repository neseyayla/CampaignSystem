import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import { Campaign, CampaignCriteria, CreateCampaign } from '../models/campaign';

/**
 * Everything the application knows about talking to the campaign endpoints.
 *
 * Components do not call HttpClient themselves. It is the same separation the server side
 * keeps between controllers and services: if the route changes, or a header has to be added
 * to every request, there is one place to change it.
 */
@Injectable({ providedIn: 'root' })
export class CampaignService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/campaigns`;

  getAll(): Observable<Campaign[]> {
    return this.http.get<Campaign[]>(this.baseUrl);
  }

  getById(id: number): Observable<Campaign> {
    return this.http.get<Campaign>(`${this.baseUrl}/${id}`);
  }

  create(campaign: CreateCampaign): Observable<Campaign> {
    return this.http.post<Campaign>(this.baseUrl, campaign);
  }

  getCriteria(id: number): Observable<CampaignCriteria> {
    return this.http.get<CampaignCriteria>(`${this.baseUrl}/${id}/criteria`);
  }

  /**
   * Replaces the campaign's whole scope. Anything left out of the lists is removed, so the
   * form always sends the complete picture rather than a difference.
   */
  setCriteria(id: number, criteria: CampaignCriteria): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/criteria`, criteria);
  }
}
