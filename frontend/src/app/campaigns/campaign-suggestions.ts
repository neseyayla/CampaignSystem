import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { RecommendationService } from '../services/recommendation.service';
import { CampaignSuggestion } from '../models/recommendation';

const MONTH_NAMES = [
  'Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz', 'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara'
];

/**
 * Read-only list of campaign ideas, ranked by the server from recent transaction history.
 * Each row's button carries the suggestion's draft to the new-campaign form as router
 * state, where it prefills the fields the engine has an opinion on.
 */
@Component({
  selector: 'app-campaign-suggestions',
  imports: [DecimalPipe],
  templateUrl: './campaign-suggestions.html',
  styleUrl: './campaign-suggestions.css'
})
export class CampaignSuggestions {
  private readonly recommendationService = inject(RecommendationService);
  private readonly router = inject(Router);

  protected readonly suggestions = signal<CampaignSuggestion[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Off by default: the point of the screen is the gaps, not the categories already covered.
  protected readonly includeCovered = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.recommendationService
      .getSuggestions({ includeCovered: this.includeCovered() })
      .subscribe({
        next: suggestions => {
          this.suggestions.set(suggestions);
          this.loading.set(false);
        },
        // "No suggestions" and "the API is down" must not look the same.
        error: () => {
          this.error.set('Sunucuya ulaşılamadı. API çalışıyor mu?');
          this.loading.set(false);
        }
      });
  }

  protected toggleCovered(): void {
    this.includeCovered.update(value => !value);
    this.load();
  }

  protected trendPercent(ratio: number | null): string {
    if (ratio === null) {
      return '—';
    }

    const percent = Math.round(ratio * 100);

    return `${percent > 0 ? '+' : ''}${percent}%`;
  }

  protected months(list: number[]): string {
    return list.map(month => MONTH_NAMES[month - 1] ?? String(month)).join(', ');
  }

  protected createFrom(suggestion: CampaignSuggestion): void {
    this.router.navigate(['/campaigns/new'], { state: { campaignDraft: suggestion.draft } });
  }
}
