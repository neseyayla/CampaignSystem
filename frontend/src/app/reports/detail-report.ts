import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { Campaign } from '../models/campaign';
import { CampaignService } from '../services/campaign.service';
import {
  DetailReportFilters,
  DetailReportRow,
  DetailReportType,
  ReportsService
} from '../services/reports.service';

@Component({
  selector: 'app-detail-report',
  imports: [DatePipe, DecimalPipe, FormsModule],
  templateUrl: './detail-report.html',
  styleUrl: './detail-report.css'
})
export class DetailReport {
  private readonly service = inject(ReportsService);
  private readonly campaignService = inject(CampaignService);

  protected readonly campaigns = signal<Campaign[]>([]);
  protected readonly rows = signal<DetailReportRow[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  // Whether a fetch has run, so "Kayıt bulunamadı" only shows after a query, not on first open.
  protected readonly fetched = signal(false);

  // Draft (radio) vs applied: the table reflects the applied type, so picking a radio does not
  // change the columns until "Getir" is pressed.
  protected readonly reportType = signal<DetailReportType>('Loaded');
  protected readonly appliedType = signal<DetailReportType>('Loaded');
  protected readonly campaignId = signal<number | null>(null);
  protected readonly customerNumber = signal('');
  protected readonly cardId = signal('');

  protected readonly reportTypes: { value: DetailReportType; label: string }[] = [
    { value: 'Loaded', label: 'Yükleme Raporu' },
    { value: 'UnusedClawback', label: 'Geri Alım Raporu' },
    { value: 'RefundClawback', label: 'İade Raporu' },
    { value: 'Transaction', label: 'İşlem Raporu' }
  ];

  protected readonly isTransaction = computed(() => this.appliedType() === 'Transaction');

  // The points column's header changes with the applied report type; the transaction report money.
  protected readonly amountHeader = computed(() => {
    switch (this.appliedType()) {
      case 'UnusedClawback': return 'Geri Alınan Puan';
      case 'RefundClawback': return 'İade Edilen Puan';
      case 'Transaction': return 'İşlem Tutarı';
      default: return 'Yüklenen Puan';
    }
  });

  constructor() {
    this.campaignService.getAll().subscribe({
      // Only campaigns that have reached the loading stage carry report data; a still-running
      // (Pending/Ongoing) campaign has no rewards yet, so reporting on it makes no sense.
      next: list => this.campaigns.set(
        (list ?? []).filter(c => c.status === 'Loading' || c.status === 'Ended')
      ),
      error: () => this.campaigns.set([])
    });
  }

  /** Fetch the chosen report with the current filters. */
  protected run(): void {
    this.loading.set(true);
    this.error.set(null);
    this.fetched.set(true);
    // The table columns follow the applied type, set only now (not on radio click).
    this.appliedType.set(this.reportType());

    this.service.getDetail(this.reportType(), this.filters()).subscribe({
      next: rows => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Rapor alınamadı. API çalışıyor mu?');
        this.rows.set([]);
        this.loading.set(false);
      }
    });
  }

  /** Reset the filters and clear the results (keeps the chosen report type). */
  protected clear(): void {
    this.campaignId.set(null);
    this.customerNumber.set('');
    this.cardId.set('');
    this.rows.set([]);
    this.fetched.set(false);
    this.error.set(null);
  }

  /** Download the current rows as a CSV that Turkish Excel opens correctly. */
  protected exportCsv(): void {
    const rows = this.rows();
    if (rows.length === 0) return;

    const money = (n: number) => n.toLocaleString('tr-TR', { maximumFractionDigits: 2 });
    const day = (iso: string) => {
      const [year, month, date] = iso.split('T')[0].split('-');
      return `${date}.${month}.${year}`;
    };
    const cell = (value: string) => `"${value.replace(/"/g, '""')}"`;

    const header = this.isTransaction()
      ? ['Müşteri No', 'Kart No', 'İşlem Tutarı', 'İşlem Tarihi', 'Üye']
      : ['Müşteri No', 'Kart No', 'Kampanya', 'Tarih', this.amountHeader()];

    const toCells = (r: DetailReportRow): string[] =>
      this.isTransaction()
        ? [r.customerNumber, r.cardId?.toString() ?? '', money(r.amount), day(r.date), r.merchantName ?? '']
        : [r.customerNumber, r.cardId?.toString() ?? '', r.campaignName ?? '', day(r.date), money(r.amount)];

    const csv = [header, ...rows.map(toCells)]
      .map(cols => cols.map(cell).join(';'))
      .join('\r\n');

    // The BOM makes Excel read it as UTF-8 (so Turkish characters survive).
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const label = this.reportTypes.find(t => t.value === this.appliedType())?.label ?? 'rapor';
    link.href = url;
    link.download = `${label}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private filters(): DetailReportFilters {
    const cardText = this.cardId().trim();
    const cardId = cardText.length > 0 ? Number(cardText) : null;
    return {
      campaignId: this.campaignId(),
      customerNumber: this.customerNumber().trim() || null,
      cardId: cardId !== null && Number.isFinite(cardId) ? cardId : null
    };
  }
}
