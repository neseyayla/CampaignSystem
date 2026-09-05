import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import { CampaignSuggestion, RecommendationQuery } from '../models/recommendation';

/**
 * The campaign suggestion endpoint. Read-only: it changes nothing, it ranks merchant
 * categories worth defining a campaign over.
 *
 * Components do not call HttpClient themselves — same split the rest of the app keeps.
 */
@Injectable({ providedIn: 'root' })
export class RecommendationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/campaign-recommendations`;

  getSuggestions(query: RecommendationQuery = {}): Observable<CampaignSuggestion[]> {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null) {
        params = params.set(key, String(value));
      }
    }

    return this.http.get<CampaignSuggestion[]>(this.baseUrl, { params });
  }
}
