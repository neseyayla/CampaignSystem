import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, forkJoin, of, switchMap } from 'rxjs';

import { CampaignService } from '../services/campaign.service';
import { LookupService } from '../services/lookup.service';
import {
  Campaign,
  CampaignCondition,
  CampaignConditionsPreviewRequest,
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

  protected readonly conditions = signal<CampaignCondition[]>([]);
  protected readonly conditionsSaving = signal(false);
  protected readonly conditionsNotice = signal<string | null>(null);

  /**
   * The same auto-generated sentences a saved campaign would get, kept live while a new one
   * is still being filled in — there is no id yet to call .../conditions/generate with.
   */
  protected readonly conditionsPreview = signal<string[]>([]);

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

    // Keeps the "new campaign" preview in step with the rule fields (amounts, dates,
    // gender…), debounced so it does not call the API on every keystroke.
    this.form.valueChanges
      .pipe(debounceTime(300))
      .subscribe(() => this.refreshConditionsPreview());

    // And with the criteria pickers, which are signals rather than form controls and so do
    // not appear in valueChanges.
    effect(() => {
      this.selectedSegments();
      this.selectedProducts();
      this.selectedMerchants();
      this.selectedTransactionCodes();

      this.refreshConditionsPreview();
    });
  }

  /**
   * Recomputes the "new campaign" preview from the form's current values. A no-op once the
   * campaign is saved — from then on the "Kampanya Koşulları" section below, backed by the
   * real generate/save endpoints, takes over.
   */
  private refreshConditionsPreview(): void {
    if (this.editing()) {
      return;
    }

    const value = this.form.getRawValue();

    const request: CampaignConditionsPreviewRequest = {
      campaignType: value.campaignType,
      earningType: value.earningType,
      gender: (value.gender || null) as Gender | null,
      cardType: (value.cardType || null) as CardType | null,
      startDate: value.startDate ? value.startDate + 'T00:00:00' : null,
      endDate: value.endDate ? value.endDate + 'T23:59:59' : null,
      minimumAmount: value.minimumAmount,
      maximumAmount: value.maximumAmount,
      rewardPoint: value.rewardPoint,
      maxRewardAmount: value.maxRewardAmount,
      criteria: {
        segmentIds: this.selectedSegments(),
        productIds: this.selectedProducts(),
        merchantIds: this.selectedMerchants(),
        transactionCodeIds: this.selectedTransactionCodes()
      }
    };

    this.campaignService.previewConditions(request).subscribe({
      next: list => this.conditionsPreview.set(list),
      // A stray 400 while the form is mid-edit is not worth surfacing — the preview just
      // stays as it was until the next valid change comes through.
      error: () => {}
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

    this.conditions.set([]);
    this.conditionsNotice.set(null);
    this.conditionsPreview.set([]);
  }

  // Loading and saving -------------------------------------------------------

  private load(id: number): void {
    this.error.set(null);
    this.notice.set(null);

    forkJoin({
      campaign: this.campaignService.getById(id),
      criteria: this.campaignService.getCriteria(id),
      conditions: this.campaignService.getConditions(id)
    }).subscribe({
      next: result => {
        this.campaignId.set(result.campaign.id);
        this.status.set(result.campaign.status);
        this.fill(result.campaign, result.criteria);
        this.conditions.set(result.conditions);
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
    //
    // A brand new campaign also gets its terms generated right away, once its rules and
    // criteria are both in place — that first draft is what "Kampanya Koşulları" shows the
    // moment the record reopens. An update does not repeat this: an operator may already
    // have edited those lines by hand, and saving the form is not the moment to overwrite
    // them again — that is what the "Yeniden Oluştur" button below is for.
    const saved$ =
      id === null
        ? this.campaignService.create(campaign).pipe(
            switchMap(created =>
              this.campaignService.setCriteria(created.id, criteria).pipe(
                switchMap(() => this.campaignService.generateConditions(created.id)),
                switchMap(() => of(created.id))
              )
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

  // Conditions -----------------------------------------------------------------

  /** A free line the operator is adding by hand, not one the system derived. */
  protected addCondition(): void {
    this.conditions.update(list => [
      ...list,
      { id: 0, text: '', displayOrder: list.length, isAutoGenerated: false }
    ]);
  }

  protected removeCondition(index: number): void {
    this.conditions.update(list => list.filter((_, i) => i !== index));
  }

  protected moveConditionUp(index: number): void {
    if (index === 0) {
      return;
    }

    this.conditions.update(list => {
      const next = [...list];
      [next[index - 1], next[index]] = [next[index], next[index - 1]];
      return next;
    });
  }

  protected moveConditionDown(index: number): void {
    this.conditions.update(list => {
      if (index >= list.length - 1) {
        return list;
      }

      const next = [...list];
      [next[index], next[index + 1]] = [next[index + 1], next[index]];
      return next;
    });
  }

  protected editCondition(index: number, text: string): void {
    this.conditions.update(list => list.map((c, i) => (i === index ? { ...c, text } : c)));
  }

  /** Rewrites the auto-generated lines from the campaign's current rules and criteria. */
  protected regenerateConditions(): void {
    const id = this.campaignId();

    if (id === null) {
      return;
    }

    this.conditionsSaving.set(true);
    this.conditionsNotice.set(null);

    this.campaignService.generateConditions(id).subscribe({
      next: list => {
        this.conditions.set(list);
        this.conditionsSaving.set(false);
        this.conditionsNotice.set('Koşullar kampanya kurallarından yeniden oluşturuldu.');
      },
      error: () => {
        this.conditionsSaving.set(false);
        this.conditionsNotice.set('Koşullar oluşturulamadı.');
      }
    });
  }

  protected saveConditions(): void {
    const id = this.campaignId();

    if (id === null) {
      return;
    }

    this.conditionsSaving.set(true);
    this.conditionsNotice.set(null);

    this.campaignService.setConditions(id, this.conditions()).subscribe({
      next: () => {
        this.conditionsSaving.set(false);
        this.conditionsNotice.set('Koşullar kaydedildi.');
        this.loadConditions(id);
      },
      error: () => {
        this.conditionsSaving.set(false);
        this.conditionsNotice.set('Koşullar kaydedilemedi.');
      }
    });
  }

  private loadConditions(id: number): void {
    this.campaignService.getConditions(id).subscribe({ next: list => this.conditions.set(list) });
  }
}
