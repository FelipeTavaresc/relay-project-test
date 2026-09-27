import { Pipe, PipeTransform } from '@angular/core';

const MonthNames = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
] as const;

/**
 * Formats the API's `yyyy-MM-dd` values for display. Deliberately string arithmetic rather than
 * `DatePipe`: these dates are already the *account's* local calendar dates, and constructing a
 * `Date` from them would reinterpret them in the browser's zone and shift them by a day.
 */
@Pipe({ name: 'localDate' })
export class LocalDatePipe implements PipeTransform {
  transform(isoDate: string): string {
    const parts = isoDate.split('-');
    const [year, month, day] = parts;

    if (parts.length !== 3 || year === undefined || month === undefined || day === undefined) {
      return isoDate;
    }

    const name = MonthNames[Number(month) - 1];

    return name === undefined ? isoDate : `${Number(day)} ${name} ${year}`;
  }
}
