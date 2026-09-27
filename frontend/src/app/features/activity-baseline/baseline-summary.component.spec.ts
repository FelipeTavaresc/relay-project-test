import { TestBed } from '@angular/core/testing';

import { ActivityBaselineResponse } from './activity-baseline.models';
import { BaselineSummaryComponent } from './baseline-summary.component';

/** Account 6 at 30 days, as techspec § API Endpoints documents the payload. */
const report: ActivityBaselineResponse = {
  accountId: 6,
  accountName: 'Metro Collision Centers',
  timezone: 'America/New_York',
  windowDays: 30,
  windowStart: '2026-06-28',
  windowEnd: '2026-07-27',
  summary: { recentTotal: 175, baselineTotal: 311.75, absoluteDelta: -136.75, percentDelta: -43.87 },
  locations: [],
};

describe('BaselineSummaryComponent', () => {
  it('renders the account line from a single object literal', () => {
    const fixture = TestBed.createComponent(BaselineSummaryComponent);
    fixture.componentRef.setInput('report', report);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Metro Collision Centers');
    expect(text).toContain('Last 30 days');
    expect(text).toContain('28 Jun 2026 to 27 Jul 2026');
    expect(text).toContain('175');
    expect(text).toContain('311.75');
    expect(text).toContain('-136.75');
    expect(text).toContain('-43.87%');
  });
});
