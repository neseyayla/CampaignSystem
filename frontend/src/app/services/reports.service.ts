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

/** Which detailed, row-level report to fetch. Matches the backend DetailReportType names. */
export type DetailReportType = 'Loaded' | 'UnusedClawback' | 'RefundClawback' | 'Transaction';

/** Filters for the detailed report; all optional. */
export interface DetailReportFilters {
  campaignId?: number | null;
  customerNumber?: string | null;
  cardId?: number | null;
}

/** One row of a detailed report; the meaningful columns depend on the report type. */
export interface DetailReportRow {
  customerNumber: string;
  cardId: number | null;
  campaignName: string | null;
  date: string;
  amount: number;
  merchantName: string | null;
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

  /** A detailed, row-level report of the given type, narrowed by the optional filters. */
  getDetail(type: DetailReportType, filters: DetailReportFilters = {}): Observable<DetailReportRow[]> {
    let params = new HttpParams().set('type', type);
    if (filters.campaignId != null) params = params.set('campaignId', filters.campaignId);
    if (filters.customerNumber) params = params.set('customerNumber', filters.customerNumber);
    if (filters.cardId != null) params = params.set('cardId', filters.cardId);

    return this.http.get<DetailReportRow[]>(`${this.baseUrl}/detail`, { params });
  }
}
