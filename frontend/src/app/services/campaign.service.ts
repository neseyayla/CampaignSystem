import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import {
  Campaign,
  CampaignCondition,
  CampaignCriteria,
  CreateCampaign,
  UpdateCampaign
} from '../models/campaign';

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

  /** Returns 204, so there is no body to read back — reload the campaign if it is needed. */
  update(id: number, campaign: UpdateCampaign): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, campaign);
  }

  /**
   * Soft delete on the server: the row stays and its IsActive flag is cleared, so the rewards
   * and enrolments that point at the campaign keep their meaning. It simply stops appearing
   * in the list.
   */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
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

  getConditions(id: number): Observable<CampaignCondition[]> {
    return this.http.get<CampaignCondition[]>(`${this.baseUrl}/${id}/conditions`);
  }

  /** Replaces the campaign's whole set of terms with the one given. Returns 204. */
  setConditions(id: number, conditions: CampaignCondition[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/conditions`, conditions);
  }

  /**
   * Rebuilds the campaign's terms from its current rules and criteria. Only replaces the
   * previously auto-generated lines — anything typed in by hand stays.
   */
  generateConditions(id: number): Observable<CampaignCondition[]> {
    return this.http.post<CampaignCondition[]>(`${this.baseUrl}/${id}/conditions/generate`, {});
  }
}
