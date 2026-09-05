import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CampaignService } from '../services/campaign.service';
import { Campaign } from '../models/campaign';
import { campaignTypeLabels, earningTypeLabels, statusLabels } from '../models/labels';

@Component({
  selector: 'app-campaign-list',
  imports: [DatePipe, RouterLink],
  templateUrl: './campaign-list.html',
  styleUrl: './campaign-list.css'
})
export class CampaignList {
  private readonly campaignService = inject(CampaignService);

  // Signals rather than plain fields: the application runs zoneless, so nothing watches for
  // changes on its own. Reading a signal in the template is what ties the two together.
  protected readonly campaigns = signal<Campaign[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly campaignTypeLabels = campaignTypeLabels;
  protected readonly earningTypeLabels = earningTypeLabels;
  protected readonly statusLabels = statusLabels;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.campaignService.getAll().subscribe({
      next: campaigns => {
        this.campaigns.set(campaigns);
        this.loading.set(false);
      },
      // An empty table would say "there are no campaigns", which is a different statement
      // from "the API could not be reached". They must not look the same.
      error: () => {
        this.error.set('Sunucuya ulaşılamadı. API çalışıyor mu?');
        this.loading.set(false);
      }
    });
  }
}
