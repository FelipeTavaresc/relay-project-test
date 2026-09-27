import { Pipe, PipeTransform } from '@angular/core';

const Format = new Intl.NumberFormat('en-US', {
  signDisplay: 'exceptZero',
  maximumFractionDigits: 2,
});

/** An absolute delta, signed. Reported even where a percentage is not (FR8). */
@Pipe({ name: 'signedNumber' })
export class SignedNumberPipe implements PipeTransform {
  transform(value: number): string {
    return Format.format(value);
  }
}
