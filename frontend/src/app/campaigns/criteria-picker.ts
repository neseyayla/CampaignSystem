import { Component, input, model } from '@angular/core';

import { LookupOption } from '../models/lookup';

/**
 * A multi-select list of reference values, used for all four criteria dimensions.
 *
 * The lists are short — five segments, six products, a handful of merchants — so every
 * option is shown at once rather than hidden behind a dropdown. That mirrors the screen this
 * replaces, where the whole list is visible and ticking one is a single click.
 */
@Component({
  selector: 'app-criteria-picker',
  templateUrl: './criteria-picker.html',
  styleUrl: './criteria-picker.css'
})
export class CriteriaPicker {
  readonly label = input.required<string>();
  readonly options = input.required<LookupOption[]>();

  // model() rather than input() + output(): the parent binds with [(selected)] and the two
  // stay in step without wiring an event by hand.
  readonly selected = model<number[]>([]);

  protected isSelected(id: number): boolean {
    return this.selected().includes(id);
  }

  protected toggle(id: number, checked: boolean): void {
    const current = this.selected();

    this.selected.set(checked ? [...current, id] : current.filter(x => x !== id));
  }

  protected clear(): void {
    this.selected.set([]);
  }
}
