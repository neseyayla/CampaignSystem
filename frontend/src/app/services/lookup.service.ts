import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import { LookupOption, Merchant, Product, Segment, TransactionCode } from '../models/lookup';

/**
 * The four reference lists the criteria pickers are built from.
 *
 * Each endpoint returns a slightly different shape — segmentCode, productCode, code,
 * merchantNumber — so each is mapped to the one LookupOption shape here. The pickers then
 * do not have to know which list they are showing.
 */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);

  getSegments(): Observable<LookupOption[]> {
    return this.http
      .get<Segment[]>(`${API_BASE_URL}/segments`)
      .pipe(map(rows => rows.map(r => ({ id: r.id, code: r.segmentCode, name: r.segmentName }))));
  }

  getProducts(): Observable<LookupOption[]> {
    return this.http
      .get<Product[]>(`${API_BASE_URL}/products`)
      .pipe(map(rows => rows.map(r => ({ id: r.id, code: r.productCode, name: r.productName }))));
  }

  getMerchants(): Observable<LookupOption[]> {
    return this.http
      .get<Merchant[]>(`${API_BASE_URL}/merchants`)
      .pipe(map(rows => rows.map(r => ({ id: r.id, code: r.merchantNumber, name: r.merchantName }))));
  }

  getTransactionCodes(): Observable<LookupOption[]> {
    return this.http
      .get<TransactionCode[]>(`${API_BASE_URL}/transaction-codes`)
      .pipe(map(rows => rows.map(r => ({ id: r.id, code: r.code, name: r.name }))));
  }
}
