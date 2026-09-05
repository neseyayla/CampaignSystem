import { Component, computed, input, model, signal } from '@angular/core';

import { LookupOption } from '../models/lookup';

/**
 * A multi-select list of reference values, used for all four criteria dimensions.
 *
 * Selected options show as removable chips in a box on the form itself; the full option
 * list only appears in a popup, opened with "Listele", so the form stays short even when
 * a dimension has many options.
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

  protected readonly open = signal(false);

  protected readonly selectedItems = computed(() => {
    const ids = this.selected();
    return this.options()
      .filter(option => ids.includes(option.id))
      .sort((a, b) => (a.code < b.code ? -1 : a.code > b.code ? 1 : 0));
  });

  protected isSelected(id: number): boolean {
    return this.selected().includes(id);
  }

  protected toggle(id: number, checked: boolean): void {
    const current = this.selected();

    this.selected.set(checked ? [...current, id] : current.filter(x => x !== id));
  }

  protected remove(id: number): void {
    this.selected.set(this.selected().filter(x => x !== id));
  }

  protected openModal(): void {
    this.open.set(true);
  }

  protected closeModal(): void {
    this.open.set(false);
  }
}
