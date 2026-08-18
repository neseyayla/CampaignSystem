/**
 * The reference tables a campaign's criteria point at. All four are seeded by a migration,
 * so they exist on every installation and the ids are stable.
 */

export interface Segment {
  id: number;
  segmentCode: string;
  segmentName: string;
}

export interface Product {
  id: number;
  productCode: string;
  productName: string;
}

export interface Merchant {
  id: number;
  merchantNumber: string;
  merchantName: string;
  isActive: boolean;
}

export interface TransactionCode {
  id: number;
  code: string;
  name: string;
}

/**
 * What the criteria pickers actually need: something to send, something to show.
 * Keeping one shape means one picker component works for all four lists.
 */
export interface LookupOption {
  id: number;
  code: string;
  name: string;
}
