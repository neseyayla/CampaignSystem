import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';

import { CampaignService } from '../services/campaign.service';
import { LookupService } from '../services/lookup.service';
import {
  Campaign,
  CampaignCriteria,
  CampaignType,
  CardType,
  CreateCampaign,
  EarningType,
  Gender
} from '../models/campaign';
import { LookupOption } from '../models/lookup';
import { CriteriaPicker } from './criteria-picker';

/**
 * Campaign definition screen.
 *
 * One component serves both a new campaign and an existing one; which it is follows from
 * whether the route carries an id. That single fact decides whether saving creates a
 * record or overwrites one, so there is no separate edit screen to keep in step.
 */
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
  private readonly route = inject(ActivatedRoute);

  protected readonly segments = signal<LookupOption[]>([]);
  protected readonly products = signal<LookupOption[]>([]);
  protected readonly merchants = signal<LookupOption[]>([]);
  protected readonly transactionCodes = signal<LookupOption[]>([]);

  protected readonly selectedSegments = signal<number[]>([]);
  protected readonly selectedProducts = signal<number[]>([]);
  protected readonly selectedMerchants = signal<number[]>([]);
  protected readonly selectedTransactionCodes = signal<number[]>([]);

  /** The campaign being edited, or null while a new one is being entered. */
  protected readonly campaignId = signal<number | null>(null);
  protected readonly status = signal<string | null>(null);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly editing = computed(() => this.campaignId() !== null);

  /**
   * True once Sil has been pressed and before it is confirmed. Deleting takes two presses
   * rather than a browser confirm box, so the question stays on the screen it belongs to and
   * a stray click cannot remove a campaign.
   */
  protected readonly confirmingDelete = signal(false);


  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(1000)],
    // Typed as the full union rather than the default literal, so loading an existing
    // campaign can put any of the values back into the control.
    campaignType: ['Mass' as CampaignType, Validators.required],
    earningType: ['CardBased' as EarningType, Validators.required],

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

    // paramMap rather than a snapshot: moving from one campaign to another reuses this
    // component, and a snapshot would keep showing the first one.
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');

      if (id) {
        this.load(Number(id));
      } else {
        this.clear();
      }
    });
  }

  /**
   * One button for both cases: a record that is open is written over, a new one is created.
   * The id in the route is what decides which, so the screen never has two save buttons that
   * do almost the same thing.
   */
  protected save(): void {
    this.submit(this.campaignId());
  }

  protected askDelete(): void {
    this.confirmingDelete.set(true);
  }

  protected cancelDelete(): void {
    this.confirmingDelete.set(false);
  }

  protected confirmDelete(): void {
    const id = this.campaignId();

    if (id === null) {
      return;
    }

    this.confirmingDelete.set(false);
    this.saving.set(true);

    this.campaignService.delete(id).subscribe({
      next: () => this.router.navigate(['/campaigns']),
      error: () => {
        this.saving.set(false);
        this.error.set('Kampanya silinemedi.');
      }
    });
  }

  protected cancel(): void {
    this.router.navigate(['/campaigns']);
  }

  /** Leaves edit mode and empties every field. Also runs when the route carries no id. */
  private clear(): void {
    this.campaignId.set(null);
    this.status.set(null);
    this.error.set(null);
    this.notice.set(null);

    this.form.reset({
      name: '',
      description: '',
      campaignType: 'Mass',
      earningType: 'CardBased',
      gender: '',
      cardType: '',
      startDate: '',
      endDate: '',
      minimumAmount: null,
      maximumAmount: null,
      rewardPoint: null,
      maxRewardAmount: null
    });

    this.selectedSegments.set([]);
    this.selectedProducts.set([]);
    this.selectedMerchants.set([]);
    this.selectedTransactionCodes.set([]);
  }

  // Loading and saving -------------------------------------------------------

  private load(id: number): void {
    this.error.set(null);
    this.notice.set(null);

    forkJoin({
      campaign: this.campaignService.getById(id),
      criteria: this.campaignService.getCriteria(id)
    }).subscribe({
      next: result => {
        this.campaignId.set(result.campaign.id);
        this.status.set(result.campaign.status);
        this.fill(result.campaign, result.criteria);
      },
      error: () => {
        this.campaignId.set(null);
        this.error.set(id + ' numaralı kampanya bulunamadı.');
      }
    });
  }

  private fill(campaign: Campaign, criteria: CampaignCriteria): void {
    this.form.patchValue({
      name: campaign.name,
      description: campaign.description ?? '',
      campaignType: campaign.campaignType,
      earningType: campaign.earningType,
      gender: campaign.gender ?? '',
      cardType: campaign.cardType ?? '',

      // The API sends a full timestamp; a date input only accepts the day part.
      startDate: campaign.startDate.substring(0, 10),
      endDate: campaign.endDate.substring(0, 10),

      minimumAmount: campaign.minimumAmount,
      maximumAmount: campaign.maximumAmount,
      rewardPoint: campaign.rewardPoint,
      maxRewardAmount: campaign.maxRewardAmount
    });

    this.selectedSegments.set(criteria.segmentIds);
    this.selectedProducts.set(criteria.productIds);
    this.selectedMerchants.set(criteria.merchantIds);
    this.selectedTransactionCodes.set(criteria.transactionCodeIds);
  }

  /** Writes the form: creates a campaign when id is null, updates it otherwise. */
  private submit(id: number | null): void {
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
    this.notice.set(null);

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
      startDate: value.startDate + 'T00:00:00',
      endDate: value.endDate + 'T23:59:59',

      minimumAmount: value.minimumAmount,
      maximumAmount: value.maximumAmount,
      rewardPoint: value.rewardPoint,
      maxRewardAmount: value.maxRewardAmount
    };

    const criteria: CampaignCriteria = {
      segmentIds: this.selectedSegments(),
      productIds: this.selectedProducts(),
      merchantIds: this.selectedMerchants(),
      transactionCodeIds: this.selectedTransactionCodes()
    };

    // The campaign has to exist before its criteria can point at it, so the criteria call
    // waits for the id — which create supplies in its response and update already knows.
    const saved$ =
      id === null
        ? this.campaignService.create(campaign).pipe(
            switchMap(created =>
              this.campaignService
                .setCriteria(created.id, criteria)
                .pipe(switchMap(() => of(created.id)))
            )
          )
        : this.campaignService.update(id, campaign).pipe(
            switchMap(() =>
              this.campaignService.setCriteria(id, criteria).pipe(switchMap(() => of(id)))
            )
          );

    saved$.subscribe({
      next: savedId => {
        this.saving.set(false);

        // Staying on the record after saving is what the original screen does: the operator
        // usually wants to see what was written rather than be sent back to a list.
        this.notice.set(
          id === null ? 'Kampanya kaydedildi (No: ' + savedId + ').' : 'Kampanya güncellendi.'
        );

        this.router.navigate(['/campaigns', savedId]);
      },
      error: response => {
        this.saving.set(false);
        this.error.set(
          typeof response.error === 'string' ? response.error : 'Kampanya kaydedilemedi.'
        );
      }
    });
  }

  protected invalid(field: string): boolean {
    const control = this.form.get(field);
    return !!control && control.invalid && control.touched;
  }
}
