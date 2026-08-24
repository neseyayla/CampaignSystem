/**
 * The shapes the customer endpoints return.
 *
 * Narrower than the staff application's models on purpose: the campaign's lifecycle
 * status, its active flag and the ids behind its criteria never reach this screen, so
 * there is nothing here to describe them.
 */

export type EarningType = 'CardBased' | 'CustomerBased';

export type CardType = 'Primary' | 'Supplementary';

export interface CustomerCampaign {
  campaignId: number;
  name: string;
  description: string | null;
  startDate: string;
  endDate: string;

  /** False for a campaign that has not begun yet. Those are listed, not hidden. */
  hasStarted: boolean;

  /** True when only the transactions of customers who signed up are counted. */
  enrollmentRequired: boolean;

  /** Always false for a campaign that needs no enrolment. */
  enrolled: boolean;

  /** Decides whether enrolling means picking a card or signing up as a person. */
  earningType: EarningType;

  rewardPoint: number | null;
  maxRewardAmount: number | null;
  minimumAmount: number | null;
  maximumAmount: number | null;

  /** Empty means the spending counts anywhere. */
  merchants: string[];

  /** Empty means every kind of transaction counts. */
  transactionCodes: string[];

  /** The campaign's terms, in display order, ready to show under "Kampanya Koşulları". */
  conditions: string[];
}

export interface RewardPreviewLine {
  /** Null when the campaign pools every card into one figure. */
  cardId: number | null;
  qualifyingCount: number;
  earnedBeforeCap: number;
  rewardPoint: number;

  /** True when the campaign's ceiling held the reward below what the spending earned. */
  capApplied: boolean;
}

/** What the customer would be paid if the campaign were settled right now. */
export interface RewardPreview {
  campaignId: number;
  customerId: number;
  lines: RewardPreviewLine[];
  totalRewardPoint: number;
}

export interface CustomerCampaignDetail {
  campaign: CustomerCampaign;
  progress: RewardPreview;
}

/** One finished campaign the customer has already been paid for. */
export interface RewardCampaignLine {
  campaignId: number;
  campaignName: string;
  rewardRows: number;
  qualifyingCount: number;
  totalRewardPoint: number;
  rewardDate: string;
}

export interface RewardSummary {
  customerId: number;
  customerNumber: string;
  totalRewardPoint: number;
  campaigns: RewardCampaignLine[];
}

export interface Card {
  id: number;
  customerId: number;
  productId: number;
  cardType: CardType | null;
  isActive: boolean;
}

export interface Product {
  id: number;
  productName: string;
}

export interface CustomerProfile {
  customerNumber: string;
  gender: 'Male' | 'Female';
  /** Null when the customer is in no segment. */
  segmentName: string | null;
  cardCount: number;
}
