import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';

/**
 * One row of the campaign report table. The two clawback figures are kept apart:
 * refundClawbackPoints were taken back because a purchase was refunded, unusedClawbackPoints
 * because the points were never redeemed.
 */
export interface CampaignReportSummary {
  campaignId: number;
  campaignName: string;
  customerCount: number;
  totalPointsEarned: number;
  refundClawbackPoints: number;
  unusedClawbackPoints: number;
  netPoints: number;
  /** The campaign's own settings — what its definition screen turned on. */
  refundClawbackEnabled: boolean;
  unusedClawbackEnabled: boolean;
}

/** The summary-card totals for the campaigns matching the current filters. */
export interface ReportTotals {
  campaignCount: number;
  distinctCustomerCount: number;
  totalPointsEarned: number;
  totalPointsClawedBack: number;
  netPoints: number;
}

/** The report payload: table rows plus the totals for the summary cards. */
export interface CampaignReportResult {
  campaigns: CampaignReportSummary[];
  totals: ReportTotals;
}

/** How the report is narrowed by a campaign's clawback settings. */
export type ClawbackFilter = 'all' | 'refund' | 'unused' | 'both';

/** The kind of a ledger line — points, except 'refund' which is money (TL). */
export type LedgerLineType = 'earn' | 'refundClawback' | 'unusedClawback' | 'refund';

/** One aggregated line of a campaign's ledger summary. */
export interface CampaignLedgerLine {
  type: LedgerLineType;
  count: number;
  total: number;
  firstDate: string | null;
  lastDate: string | null;
}

/** The report filters, sent to the server so the narrowing happens in the query, not here. */
export interface ReportFilters {
  campaignId?: number | null;
  clawback?: ClawbackFilter;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/reports`;

  /** Report rows and totals for the campaigns matching the filters. */
  getSummaries(filters: ReportFilters = {}): Observable<CampaignReportResult> {
    let params = new HttpParams();
    if (filters.campaignId != null) params = params.set('campaignId', filters.campaignId);
    if (filters.clawback && filters.clawback !== 'all') params = params.set('clawback', filters.clawback);

    return this.http.get<CampaignReportResult>(`${this.baseUrl}/campaigns`, { params });
  }

  /** A single campaign's ledger summary (loads, refunds, clawbacks). */
  getLedger(campaignId: number): Observable<CampaignLedgerLine[]> {
    return this.http.get<CampaignLedgerLine[]>(`${this.baseUrl}/campaigns/${campaignId}/ledger`);
  }
}
