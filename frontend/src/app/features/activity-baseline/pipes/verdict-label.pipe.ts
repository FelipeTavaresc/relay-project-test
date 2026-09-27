import { Pipe, PipeTransform } from '@angular/core';

import { Verdict } from '../activity-baseline.models';

const Labels: Readonly<Record<Verdict, string>> = {
  above: 'Above typical',
  typical: 'Typical',
  below: 'Below typical',
  low_volume: 'Too low to judge',
  insufficient_history: 'Not enough history',
};

/**
 * The verdict as words. PRD FR15: a row states its verdict in text, never by colour or icon alone —
 * so this pipe, not a CSS class, is what carries the meaning.
 */
@Pipe({ name: 'verdictLabel' })
export class VerdictLabelPipe implements PipeTransform {
  transform(verdict: Verdict): string {
    return Labels[verdict];
  }
}
