import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import {
  CampaignLedgerLine,
  CampaignReportSummary,
  ClawbackFilter,
  LedgerLineType,
  ReportsService,
  ReportTotals
} from '../services/reports.service';

const EMPTY_TOTALS: ReportTotals = {
  campaignCount: 0,
  distinctCustomerCount: 0,
  totalPointsEarned: 0,
  totalPointsClawedBack: 0,
  netPoints: 0
};

/** A sortable column of the report table. */
type SortKey = 'name' | 'customers' | 'earned' | 'refund' | 'unused' | 'net';

/** One active-filter chip shown under the filter bar. */
interface FilterChip {
  key: 'campaign' | 'clawback';
  label: string;
}

@Component({
  selector: 'app-report',
  imports: [DecimalPipe, FormsModule],
  templateUrl: './report.html',
  styleUrl: './report.css'
})
export class Report {
  private readonly service = inject(ReportsService);

  // Signals rather than plain fields: the application runs zoneless, so reading a signal in the
  // template is what ties a change to a re-render.
  protected readonly summaries = signal<CampaignReportSummary[]>([]);
  // The full campaign list for the dropdown, captured on first load so later filtering never
  // shrinks the options.
  protected readonly campaignOptions = signal<{ id: number; name: string }[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  // Draft state: what the filter controls hold before "Filtrele" is pressed. The campaign is a
  // free-text field with our own suggestion list, resolved to an id on apply, so it can be typed
  // or picked.
  protected readonly draftCampaignText = signal('');
  protected readonly showCampaignList = signal(false);
  protected readonly draftClawback = signal<ClawbackFilter>('all');

  // Applied state: what the current table and the chips reflect. Also what gets sent to the server.
  private readonly appliedCampaignId = signal<number | null>(null);
  private readonly appliedClawback = signal<ClawbackFilter>('all');

  protected readonly sortKey = signal<SortKey>('name');
  protected readonly sortDir = signal<'asc' | 'desc'>('asc');

  // Drill-down: the expanded campaign's ledger summary.
  protected readonly expandedId = signal<number | null>(null);
  protected readonly ledger = signal<CampaignLedgerLine[]>([]);
  protected readonly ledgerLoading = signal(false);
  protected readonly ledgerError = signal<string | null>(null);
  // The panel defaults to the ratios; "Hareketleri Gör" reveals the aggregated movement lines.
  protected readonly showLedgerLines = signal(false);

  private readonly ledgerLabels: Record<LedgerLineType, string> = {
    earn: 'Yükleme',
    refund: 'İade',
    refundClawback: 'İade sonrası geri alım',
    unusedClawback: 'Kullanılmayan puan geri alımı'
  };

  // Derived ratios from the ledger — insight the campaign row does not already show.
  protected readonly ledgerStats = computed<{ label: string; value: string }[]>(() => {
    const lines = this.ledger();
    if (lines.length === 0) return [];

    const line = (type: LedgerLineType) => lines.find(l => l.type === type);
    const earnTotal = line('earn')?.total ?? 0;
    const earnCustomers = line('earn')?.count ?? 0;
    const clawback = Math.abs(line('refundClawback')?.total ?? 0) + Math.abs(line('unusedClawback')?.total ?? 0);
    const net = earnTotal - clawback;
    const refundCount = line('refund')?.count ?? 0;
    const refundTotal = line('refund')?.total ?? 0;
    const refundClawbackCount = line('refundClawback')?.count ?? 0;

    const pct = (ratio: number | null) => ratio === null ? '—' : `%${(ratio * 100).toFixed(1)}`;
    const num = (value: number | null, suffix = '') =>
      value === null ? '—' : value.toLocaleString('tr-TR', { maximumFractionDigits: 1 }) + suffix;

    return [
      { label: 'Ortalama puan / müşteri', value: num(earnCustomers > 0 ? earnTotal / earnCustomers : null) },
      { label: 'Net puan oranı', value: pct(earnTotal > 0 ? net / earnTotal : null) },
      { label: 'Geri alım oranı (kazanılana göre)', value: pct(earnTotal > 0 ? clawback / earnTotal : null) },
      { label: 'İadelerin geri alıma dönüşme oranı', value: pct(refundCount > 0 ? refundClawbackCount / refundCount : null) },
      { label: 'Ortalama iade tutarı', value: num(refundCount > 0 ? refundTotal / refundCount : null, ' TL') }
    ];
  });

  protected readonly clawbackOptions: { value: ClawbackFilter; label: string }[] = [
    { value: 'all', label: 'Tümü' },
    { value: 'refund', label: 'İade geri alımı olanlar' },
    { value: 'unused', label: 'Kullanılmayan puan geri alımı olanlar' },
    { value: 'both', label: 'İade ve kullanılmayan geri alımı olanlar' }
  ];

  private readonly chipLabels: Record<Exclude<ClawbackFilter, 'all'>, string> = {
    refund: 'İade geri alımı var',
    unused: 'Kullanılmayan puan geri alımı var',
    both: 'İade ve kullanılmayan geri alımı var'
  };

  // Campaign suggestions matching what has been typed (substring, case-insensitive).
  protected readonly filteredCampaigns = computed(() => {
    const query = this.draftCampaignText().trim().toLocaleLowerCase('tr');
    const all = this.campaignOptions();
    if (query.length === 0) return all;
    return all.filter(c => c.name.toLocaleLowerCase('tr').includes(query));
  });

  // The rows shown: only the client-side sort is applied here — the server already narrowed the
  // data, so nothing is filtered in the browser.
  protected readonly rows = computed(() => {
    const list = [...this.summaries()];
    const dir = this.sortDir() === 'asc' ? 1 : -1;
    const key = this.sortKey();
    return list.sort((a, b) => dir * this.compare(a, b, key));
  });

  // Totals for the summary cards, computed by the server over the same filtered set so the cards
  // always match the table — the customer count here is distinct, not a sum of per-campaign counts.
  protected readonly totals = signal<ReportTotals>(EMPTY_TOTALS);

  // The active filters, as removable chips.
  protected readonly chips = computed<FilterChip[]>(() => {
    const chips: FilterChip[] = [];

    const id = this.appliedCampaignId();
    if (id !== null) {
      const name = this.campaignOptions().find(c => c.id === id)?.name ?? '';
      chips.push({ key: 'campaign', label: name });
    }

    const clawback = this.appliedClawback();
    if (clawback !== 'all') {
      chips.push({ key: 'clawback', label: this.chipLabels[clawback] });
    }

    return chips;
  });

  constructor() {
    this.load(true);
  }

  /** Update the typed campaign text and keep the suggestion list open. */
  protected onCampaignInput(value: string): void {
    this.draftCampaignText.set(value);
    this.showCampaignList.set(true);
  }

  /** Pick a campaign from the suggestions. */
  protected pickCampaign(name: string): void {
    this.draftCampaignText.set(name);
    this.showCampaignList.set(false);
  }

  /** Close the suggestion list shortly after blur, so a click on a suggestion still registers. */
  protected closeCampaignListSoon(): void {
    setTimeout(() => this.showCampaignList.set(false), 120);
  }

  /** Apply the draft filters: they become the active filters and the table is reloaded. */
  protected apply(): void {
    this.appliedCampaignId.set(this.resolveCampaignId(this.draftCampaignText()));
    this.appliedClawback.set(this.draftClawback());
    this.load();
  }

  /** Reset every filter, draft and applied, and reload the full report. */
  protected clear(): void {
    this.draftCampaignText.set('');
    this.draftClawback.set('all');
    this.appliedCampaignId.set(null);
    this.appliedClawback.set('all');
    this.load();
  }

  /** Remove one active filter from its chip, in both the draft and the applied state, and reload. */
  protected removeChip(key: FilterChip['key']): void {
    if (key === 'campaign') {
      this.draftCampaignText.set('');
      this.appliedCampaignId.set(null);
    } else {
      this.draftClawback.set('all');
      this.appliedClawback.set('all');
    }
    this.load();
  }

  // Match the typed/picked text to a campaign name (trimmed, case-insensitive). Empty or no match
  // means no campaign filter — every campaign.
  private resolveCampaignId(text: string): number | null {
    const query = text.trim().toLocaleLowerCase('tr');
    if (query.length === 0) return null;

    const match = this.campaignOptions().find(c => c.name.toLocaleLowerCase('tr') === query);
    return match?.id ?? null;
  }

  /** Expand a campaign to its ledger summary, or collapse it if already open. */
  protected toggleRow(campaignId: number): void {
    if (this.expandedId() === campaignId) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(campaignId);
    this.showLedgerLines.set(false);
    this.loadLedger();
  }

  /** Toggle between the ratios and the aggregated movement lines. */
  protected toggleLedgerLines(): void {
    this.showLedgerLines.set(!this.showLedgerLines());
  }

  /** Human label for a ledger line kind. */
  protected ledgerLabel(type: LedgerLineType): string {
    return this.ledgerLabels[type];
  }

  /** A refund is money (TL); every other line is points. */
  protected isMoney(type: LedgerLineType): boolean {
    return type === 'refund';
  }

  /** What the count is measured in: rewarded customers vs individual transactions. */
  protected countUnit(type: LedgerLineType): string {
    return type === 'earn' ? 'müşteri' : 'işlem';
  }

  /** The line's date, or a range when its movements span more than one day. Empty when none. */
  protected ledgerDate(line: CampaignLedgerLine): string {
    if (!line.firstDate) return '';
    const first = this.formatDate(line.firstDate);
    if (!line.lastDate || line.lastDate === line.firstDate) return first;
    return `${first} – ${this.formatDate(line.lastDate)}`;
  }

  private formatDate(iso: string): string {
    const [year, month, day] = iso.split('T')[0].split('-');
    return `${day}.${month}.${year}`;
  }

  private loadLedger(): void {
    const id = this.expandedId();
    if (id === null) return;

    this.ledgerLoading.set(true);
    this.ledgerError.set(null);
    this.ledger.set([]);

    this.service.getLedger(id).subscribe({
      next: lines => {
        this.ledger.set(lines);
        this.ledgerLoading.set(false);
      },
      error: () => {
        this.ledgerError.set('Özet yüklenemedi. API çalışıyor mu?');
        this.ledgerLoading.set(false);
      }
    });
  }

  private load(initial = false): void {
    this.loading.set(true);
    this.error.set(null);
    // A changed filter set can drop the expanded campaign; close the panel to avoid a stale one.
    this.expandedId.set(null);

    this.service.getSummaries({
      campaignId: this.appliedCampaignId(),
      clawback: this.appliedClawback()
    }).subscribe({
      next: result => {
        this.summaries.set(result.campaigns);
        this.totals.set(result.totals);
        // Capture the dropdown options once, from the unfiltered first load.
        if (initial) {
          this.campaignOptions.set(result.campaigns.map(r => ({ id: r.campaignId, name: r.campaignName })));
        }
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kampanyalar yüklenemedi. API çalışıyor mu?');
        this.loading.set(false);
      }
    });
  }

  /** Sort by a column; clicking the active column flips the direction. Numbers start high-to-low. */
  protected sortBy(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortKey.set(key);
      this.sortDir.set(key === 'name' ? 'asc' : 'desc');
    }
  }

  /** The arrow shown on a header: ▲/▼ on the active column, blank otherwise. */
  protected sortArrow(key: SortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? '▲' : '▼';
  }

  private compare(a: CampaignReportSummary, b: CampaignReportSummary, key: SortKey): number {
    switch (key) {
      case 'customers': return a.customerCount - b.customerCount;
      case 'earned': return a.totalPointsEarned - b.totalPointsEarned;
      case 'refund': return a.refundClawbackPoints - b.refundClawbackPoints;
      case 'unused': return a.unusedClawbackPoints - b.unusedClawbackPoints;
      case 'net': return a.netPoints - b.netPoints;
      default: return a.campaignName.localeCompare(b.campaignName, 'tr');
    }
  }
}
