/**
 * Mirrors CampaignDto on the server. The property names have to match the JSON exactly,
 * so they are camelCase here — ASP.NET Core serialises that way by default.
 *
 * The enums arrive as strings rather than numbers because the API registers
 * JsonStringEnumConverter, which is also why these are string unions and not numeric enums:
 * "Mass" is what actually comes down the wire.
 */
export type CampaignType = 'Mass' | 'EnrollmentRequired';

export type EarningType = 'CardBased' | 'CustomerBased';

export type CampaignStatus = 'Pending' | 'Ongoing' | 'Loading' | 'Ended';

export type Gender = 'Male' | 'Female';

export type CardType = 'Primary' | 'Supplementary';

export interface Campaign {
  id: number;
  name: string;
  description: string | null;
  campaignType: CampaignType;
  startDate: string;
  endDate: string;
  minimumAmount: number | null;
  maximumAmount: number | null;
  rewardPoint: number | null;
  maxRewardAmount: number | null;
  earningType: EarningType;

  /** Null when the campaign is open to every gender. */
  gender: Gender | null;

  /** Null when the campaign covers both primary and supplementary cards. */
  cardType: CardType | null;

  status: CampaignStatus;
  isActive: boolean;
}

/**
 * What the create endpoint accepts. Id, status and isActive are absent because the server
 * decides them: a new campaign is always an active draft whose status follows from its dates.
 */
export interface CreateCampaign {
  name: string;
  description: string | null;
  campaignType: CampaignType;
  earningType: EarningType;

  // Null rather than omitted: the API reads null as "no restriction", which is what an
  // empty choice on the form means.
  gender: Gender | null;
  cardType: CardType | null;

  startDate: string;
  endDate: string;
  minimumAmount: number | null;
  maximumAmount: number | null;
  rewardPoint: number | null;
  maxRewardAmount: number | null;
}

/**
 * A campaign's scope. An empty list means the campaign places no restriction on that
 * dimension — not that it matches nothing.
 */
export interface CampaignCriteria {
  segmentIds: number[];
  productIds: number[];
  merchantIds: number[];
  transactionCodeIds: number[];
}
