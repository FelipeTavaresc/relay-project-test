import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LocationBaseline } from './activity-baseline.models';
import { BaselineTableComponent } from './baseline-table.component';

/** A row as the API sends it — one object literal, which is all this component needs to render. */
const siteO: LocationBaseline = {
  location: 'Site O',
  recentTotal: 9,
  baselineTotal: 21.25,
  absoluteDelta: -12.25,
  percentDelta: -57.65,
  bandPercent: 21.69,
  verdict: 'below',
  baselineOutlier: { date: '2026-06-03', sharePercent: 41.2 },
};

describe('BaselineTableComponent', () => {
  let fixture: ComponentFixture<BaselineTableComponent>;

  const render = (locations: readonly LocationBaseline[]): string => {
    fixture.componentRef.setInput('locations', locations);
    fixture.detectChanges();

    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  };

  beforeEach(() => {
    fixture = TestBed.createComponent(BaselineTableComponent);
  });

  it('renders a row from a single object literal, verdict as text', () => {
    const text = render([siteO]);

    expect(text).toContain('Site O');
    expect(text).toContain('21.25');
    expect(text).toContain('-12.25');
    expect(text).toContain('-57.65%');
    expect(text).toContain('Below typical');
  });

  it('names the date behind an inflated baseline', () => {
    expect(render([siteO])).toContain('One day (3 Jun 2026) supplies 41.2%');
  });

  it('reads n/a rather than a percentage where the API declined to compute one', () => {
    const lowVolume: LocationBaseline = {
      ...siteO,
      location: 'Site Q',
      percentDelta: null,
      bandPercent: null,
      verdict: 'low_volume',
      baselineOutlier: null,
    };

    const text = render([lowVolume]);

    expect(text).toContain('Too low to judge');
    expect(text).toContain('n/a');
  });

  it('keeps the order the API ranked the rows in', () => {
    render([siteO, { ...siteO, location: 'Site A', percentDelta: -12.5 }]);

    const rowHeaders = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('tbody th'),
    ).map((cell) => cell.firstChild?.textContent?.trim());

    expect(rowHeaders).toEqual(['Site O', 'Site A']);
  });

  it('renders an explicit empty state rather than a blank table', () => {
    expect(render([])).toContain('No activity for this account in the selected window');
  });
});
