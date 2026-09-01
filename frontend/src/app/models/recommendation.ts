/**
 * Mirrors the recommendation DTOs on the server. camelCase to match the JSON. No enums —
 * the engine only ever points at a merchant category and a few numbers.
 */

export interface CampaignSuggestion {
  /** 1 for the strongest idea, ascending. */
  rank: number;

  /** Blended heuristic score. Comparable within one response, not across them. */
  score: number;

  merchantCategoryId: number;
  merchantCategoryName: string;

  /** A ready-to-read sentence naming why the category surfaced. */
  headline: string;

  reason: SuggestionReason;
  draft: CampaignSuggestionDraft;
}

export interface SuggestionReason {
  /** Net card spend over the lookback window, refunds subtracted. */
  totalSpend: number;

  transactionCount: number;

  /** Recent half of the window against the half before it: 0.42 is +42%. Null when uncomputable. */
  trendRatio: number | null;

  /** Average seasonal weight over the campaign's months. Above 1 is a stronger-than-usual stretch. */
  seasonalWeight: number;

  /** The months the seasonal weight was averaged over, 1-12. */
  seasonalMonths: number[];

  /** True when no open or upcoming campaign already targets the category. */
  isCoverageGap: boolean;

  /** Ids of the campaigns that already cover it, if any. */
  coveringCampaignIds: number[];
}

/**
 * A campaign skeleton built from a suggestion. The "create" button carries this to the
 * campaign form as router state; only the fields the engine has an opinion on are set.
 */
export interface CampaignSuggestionDraft {
  name: string;
  startDate: string;
  endDate: string;
  suggestedRewardPoint: number;
  merchantCategoryId: number;
  merchantIds: number[];
}

/** Optional overrides for a request; anything omitted falls back to the server defaults. */
export interface RecommendationQuery {
  lookbackDays?: number;
  horizonDays?: number;
  minimumSpend?: number;
  maxSuggestions?: number;
  includeCovered?: boolean;
}
