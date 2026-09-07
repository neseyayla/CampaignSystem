import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import {
  CampaignMovement,
  CampaignReportSummary,
  ClawbackFilter,
  MovementFilter,
  MovementType,
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
  imports: [DatePipe, DecimalPipe, FormsModule],
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

  // Drill-down: the expanded campaign's movement ledger and the active tab.
  protected readonly expandedId = signal<number | null>(null);
  protected readonly movements = signal<CampaignMovement[]>([]);
  protected readonly movementsLoading = signal(false);
  protected readonly movementsError = signal<string | null>(null);
  protected readonly movementType = signal<MovementFilter>('all');

  protected readonly movementTabs: { value: MovementFilter; label: string }[] = [
    { value: 'all', label: 'Tümü' },
    { value: 'earn', label: 'Yükleme' },
    { value: 'refund', label: 'İade' },
    { value: 'clawback', label: 'Geri Alım' }
  ];

  private readonly movementLabels: Record<MovementType, string> = {
    earn: 'Yükleme',
    refundClawback: 'İade sonrası geri alım',
    unusedClawback: 'Kullanılmayan puan geri alımı',
    refund: 'İade'
  };

  protected readonly clawbackOptions: { value: ClawbackFilter; label: string }[] = [
    { value: 'all', label: 'Tümü' },
    { value: 'refund', label: 'İade geri alımı olanlar' },
    { value: 'unused', label: 'Kullanılmayan puan geri alımı olanlar' }
  ];

  private readonly chipLabels: Record<Exclude<ClawbackFilter, 'all'>, string> = {
    refund: 'İade geri alımı var',
    unused: 'Kullanılmayan puan geri alımı var'
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

  /** Expand a campaign to its movement ledger, or collapse it if already open. */
  protected toggleRow(campaignId: number): void {
    if (this.expandedId() === campaignId) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(campaignId);
    this.movementType.set('all');
    this.loadMovements();
  }

  /** Switch the ledger tab and reload its movements. */
  protected selectMovementType(type: MovementFilter): void {
    if (this.movementType() === type) return;
    this.movementType.set(type);
    this.loadMovements();
  }

  /** Human label for a movement kind. */
  protected movementLabel(type: MovementType): string {
    return this.movementLabels[type];
  }

  /** A refund is money (TL); every other movement is points. */
  protected isMoney(type: MovementType): boolean {
    return type === 'refund';
  }

  private loadMovements(): void {
    const id = this.expandedId();
    if (id === null) return;

    this.movementsLoading.set(true);
    this.movementsError.set(null);
    this.movements.set([]);

    this.service.getMovements(id, this.movementType()).subscribe({
      next: rows => {
        this.movements.set(rows);
        this.movementsLoading.set(false);
      },
      error: () => {
        this.movementsError.set('Hareketler yüklenemedi. API çalışıyor mu?');
        this.movementsLoading.set(false);
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
