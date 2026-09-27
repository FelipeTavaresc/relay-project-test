import { Pipe, PipeTransform } from '@angular/core';

/** What a missing percentage reads as: low volume, no history, or a zero baseline (FR6, FR7, FR8). */
export const NotApplicable = 'n/a';

const Format = new Intl.NumberFormat('en-US', {
  signDisplay: 'exceptZero',
  maximumFractionDigits: 2,
});

/**
 * A percentage delta with an explicit sign, or `n/a` where the API declined to compute one. The
 * sign is the whole point of the column, so it is never left to be inferred from a minus glyph.
 */
@Pipe({ name: 'percentDelta' })
export class PercentDeltaPipe implements PipeTransform {
  transform(percent: number | null): string {
    return percent === null ? NotApplicable : `${Format.format(percent)}%`;
  }
}
