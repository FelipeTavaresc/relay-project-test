import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, FormRecord, ReactiveFormsModule } from '@angular/forms';

import {
  BaselineParams,
  DefaultWindowDays,
  WindowDays,
  WindowOptions,
} from './activity-baseline.models';

/**
 * Typed end to end: the window is one of three literals, and the location group is a `FormRecord`
 * because its keys are the account's location names and are therefore only known at runtime.
 */
type ControlsForm = FormGroup<{
  windowDays: FormControl<WindowDays>;
  locations: FormRecord<FormControl<boolean>>;
}>;

/**
 * The window radio group (PRD FR19) and the location checkboxes (FR20). Presentational: it stores
 * nothing. Both selections come down as inputs and every change goes straight back up — the URL,
 * not this component, is the store of record (task 7.0, FR21).
 */
@Component({
  selector: 'app-baseline-controls',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="form">
      <fieldset>
        <legend>Recent window</legend>
        @for (option of windowOptions; track option) {
          <label class="choice">
            <input type="radio" formControlName="windowDays" [value]="option" />
            {{ option }} days
          </label>
        }
      </fieldset>

      <fieldset formGroupName="locations">
        <legend>Locations</legend>
        @for (name of availableLocations(); track name) {
          <label class="choice">
            <input type="checkbox" [formControlName]="name" />
            {{ name }}
          </label>
        } @empty {
          <p class="note">Locations appear once the report has loaded.</p>
        }
        <p class="note">Unticking every location shows them all again.</p>
      </fieldset>
    </form>
  `,
  styles: `
    form {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
    }

    fieldset {
      border: 1px solid var(--rule);
      border-radius: 6px;
      margin: 0;
      padding: 0.5rem 0.75rem 0.75rem;
    }

    legend {
      font-size: 0.875rem;
      font-weight: 600;
      padding: 0 0.25rem;
    }

    .choice {
      align-items: center;
      display: inline-flex;
      gap: 0.35rem;
      margin: 0.25rem 1rem 0.25rem 0;
      white-space: nowrap;
    }

    .note {
      color: var(--muted);
      font-size: 0.8125rem;
      margin: 0.5rem 0 0;
    }
  `,
})
export class BaselineControlsComponent {
  /** The window currently in force. */
  readonly windowDays = input.required<WindowDays>();

  /** The locations currently shown — already resolved by the container, never an empty "means all". */
  readonly selectedLocations = input.required<readonly string[]>();

  /** Every location the account has, as the API reported them. */
  readonly availableLocations = input.required<readonly string[]>();

  readonly paramsChange = output<BaselineParams>();

  readonly windowOptions = WindowOptions;

  readonly form: ControlsForm = new FormGroup({
    windowDays: new FormControl<WindowDays>(DefaultWindowDays, { nonNullable: true }),
    locations: new FormRecord<FormControl<boolean>>({}),
  });

  constructor() {
    // A reactive form is outside-world state, which is what an effect is for. Every write below
    // passes emitEvent:false, so syncing inputs into the form never looks like a user edit and
    // input -> form -> output -> input cannot loop.
    effect(() => {
      this.form.controls.windowDays.setValue(this.windowDays(), { emitEvent: false });
      this.syncLocations(this.availableLocations(), new Set(this.selectedLocations()));
    });

    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.emitParams());
  }

  private syncLocations(available: readonly string[], selected: ReadonlySet<string>): void {
    const record = this.form.controls.locations;

    for (const name of Object.keys(record.controls)) {
      if (!available.includes(name)) {
        record.removeControl(name, { emitEvent: false });
      }
    }

    for (const name of available) {
      const checked = selected.has(name);
      const control = record.controls[name];

      if (control === undefined) {
        record.addControl(name, new FormControl(checked, { nonNullable: true }), {
          emitEvent: false,
        });
        continue;
      }

      if (control.value !== checked) {
        control.setValue(checked, { emitEvent: false });
      }
    }
  }

  private emitParams(): void {
    const { windowDays, locations } = this.form.getRawValue();
    const chosen = Object.keys(locations).filter((name) => locations[name] === true);

    // Nothing ticked is not a view the API can serve — an absent filter means every location — so
    // the ticks are restored rather than leaving the form disagreeing with the table.
    if (chosen.length === 0) {
      this.syncLocations(this.availableLocations(), new Set(this.availableLocations()));
    }

    // "All of them" is sent as no filter at all, so the URL and the request stay clean (FR20).
    const filtered = chosen.length === this.availableLocations().length ? [] : chosen;

    this.paramsChange.emit({ windowDays, locations: filtered });
  }
}
