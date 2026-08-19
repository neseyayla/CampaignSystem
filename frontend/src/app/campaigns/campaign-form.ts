import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, switchMap } from 'rxjs';

import { CampaignService } from '../services/campaign.service';
import { LookupService } from '../services/lookup.service';
import { CardType, CreateCampaign, Gender } from '../models/campaign';
import { LookupOption } from '../models/lookup';
import { CriteriaPicker } from './criteria-picker';

@Component({
  selector: 'app-campaign-form',
  imports: [ReactiveFormsModule, CriteriaPicker],
  templateUrl: './campaign-form.html',
  styleUrl: './campaign-form.css'
})
export class CampaignForm {
  private readonly formBuilder = inject(FormBuilder);
  private readonly campaignService = inject(CampaignService);
  private readonly lookupService = inject(LookupService);
  private readonly router = inject(Router);

  protected readonly segments = signal<LookupOption[]>([]);
  protected readonly products = signal<LookupOption[]>([]);
  protected readonly merchants = signal<LookupOption[]>([]);
  protected readonly transactionCodes = signal<LookupOption[]>([]);

  protected readonly selectedSegments = signal<number[]>([]);
  protected readonly selectedProducts = signal<number[]>([]);
  protected readonly selectedMerchants = signal<number[]>([]);
  protected readonly selectedTransactionCodes = signal<number[]>([]);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    campaignType: ['Mass' as const, Validators.required],
    earningType: ['CardBased' as const, Validators.required],

    // '' is the "all" option. It becomes null on the way out, which is how the API
    // expresses "no restriction" — the same rule the criteria lists follow.
    gender: [''],
    cardType: [''],
    startDate: ['', Validators.required],
    endDate: ['', Validators.required],
    minimumAmount: [null as number | null],
    maximumAmount: [null as number | null],
    rewardPoint: [null as number | null],
    maxRewardAmount: [null as number | null]
  });

  constructor() {
    // All four lists are needed before the form is usable, and they are independent, so they
    // go out together rather than one after another.
    forkJoin({
      segments: this.lookupService.getSegments(),
      products: this.lookupService.getProducts(),
      merchants: this.lookupService.getMerchants(),
      transactionCodes: this.lookupService.getTransactionCodes()
    }).subscribe({
      next: lists => {
        this.segments.set(lists.segments);
        this.products.set(lists.products);
        this.merchants.set(lists.merchants);
        this.transactionCodes.set(lists.transactionCodes);
      },
      error: () => this.error.set('Referans listeleri yüklenemedi. API çalışıyor mu?')
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    if (value.endDate <= value.startDate) {
      this.error.set('Bitiş tarihi başlangıç tarihinden sonra olmalıdır.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const campaign: CreateCampaign = {
      name: value.name,
      description: value.description || null,
      campaignType: value.campaignType,
      earningType: value.earningType,
      gender: (value.gender || null) as Gender | null,
      cardType: (value.cardType || null) as CardType | null,

      // A date input gives back a plain day. A campaign runs from the first moment of its
      // start date to the last moment of its end date, so the times are filled in here —
      // otherwise a transaction at 14:00 on the closing day would fall outside the period.
      startDate: `${value.startDate}T00:00:00`,
      endDate: `${value.endDate}T23:59:59`,

      minimumAmount: value.minimumAmount,
      maximumAmount: value.maximumAmount,
      rewardPoint: value.rewardPoint,
      maxRewardAmount: value.maxRewardAmount
    };

    // The campaign has to exist before its criteria can point at it, so the second call
    // waits for the id the first one returns.
    this.campaignService
      .create(campaign)
      .pipe(
        switchMap(created =>
          this.campaignService.setCriteria(created.id, {
            segmentIds: this.selectedSegments(),
            productIds: this.selectedProducts(),
            merchantIds: this.selectedMerchants(),
            transactionCodeIds: this.selectedTransactionCodes()
          })
        )
      )
      .subscribe({
        next: () => this.router.navigate(['/campaigns']),
        error: response => {
          this.saving.set(false);
          this.error.set(
            typeof response.error === 'string'
              ? response.error
              : 'Kampanya kaydedilemedi.'
          );
        }
      });
  }

  protected cancel(): void {
    this.router.navigate(['/campaigns']);
  }

  protected invalid(field: string): boolean {
    const control = this.form.get(field);
    return !!control && control.invalid && control.touched;
  }
}
