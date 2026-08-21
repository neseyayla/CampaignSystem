import { CampaignStatus, CampaignType, CardType, EarningType, Gender } from './campaign';

/**
 * Turkish labels for the values the API sends as English enum names.
 *
 * The wire format stays English — that is the contract, and changing it would break every
 * client. Only what the operator reads is translated, and it lives here so two screens
 * cannot end up calling the same state different things.
 */

export const campaignTypeLabels: Record<CampaignType, string> = {
  Mass: 'MASS',
  EnrollmentRequired: 'SI'
};

export const earningTypeLabels: Record<EarningType, string> = {
  CardBased: 'Kart Bazlı',
  CustomerBased: 'Müşteri Bazlı'
};

export const statusLabels: Record<CampaignStatus, string> = {
  Pending: 'Beklemede',
  Ongoing: 'Devam',
  Loading: 'Yükleme',
  Ended: 'Bitti'
};

export const genderLabels: Record<Gender, string> = {
  Male: 'Erkek',
  Female: 'Kadın'
};

export const cardTypeLabels: Record<CardType, string> = {
  Primary: 'Asıl Kart',
  Supplementary: 'Ek Kart'
};
