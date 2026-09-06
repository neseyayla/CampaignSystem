import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

import { CampaignReport, ReportsService } from '../services/reports.service';
import { CampaignService } from '../services/campaign.service';
import { Campaign } from '../models/campaign';

@Component({
  selector: 'app-report',
  imports: [FormsModule],
  templateUrl: './report.html',
  styleUrl: './report.css'
})
export class Report {
  private readonly service = inject(ReportsService);
  private readonly campaignService = inject(CampaignService);

  // Signals rather than plain fields: the application runs zoneless, so reading a signal in the
  // template is what ties a change to a re-render.
  protected readonly campaigns = signal<Campaign[]>([]);
  protected readonly selectedId = signal<number | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<CampaignReport | null>(null);

  protected readonly canSubmit = computed(() => this.selectedId() !== null && !this.loading());

  constructor() {
    this.loadCampaigns();
  }

  private loadCampaigns(): void {
    this.campaignService.getAll().subscribe({
      next: list => this.campaigns.set(list ?? []),
      error: () => this.error.set('Kampanyalar yüklenemedi. API çalışıyor mu?')
    });
  }

  protected submit(): void {
    const id = this.selectedId();
    if (id === null) return;

    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.service.getById(id).subscribe({
      next: report => {
        this.result.set(report);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(err.status === 404
          ? 'Kampanya bulunamadı.'
          : 'Rapor alınamadı. API çalışıyor mu?');
        this.loading.set(false);
      }
    });
  }
}
