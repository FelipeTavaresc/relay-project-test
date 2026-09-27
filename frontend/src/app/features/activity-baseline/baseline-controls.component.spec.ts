import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BaselineParams } from './activity-baseline.models';
import { BaselineControlsComponent } from './baseline-controls.component';

describe('BaselineControlsComponent', () => {
  let fixture: ComponentFixture<BaselineControlsComponent>;
  let emitted: BaselineParams[];

  const checkboxes = (): HTMLInputElement[] =>
    Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>(
        'input[type="checkbox"]',
      ),
    );

  const radios = (): HTMLInputElement[] =>
    Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>(
        'input[type="radio"]',
      ),
    );

  const windowLabels = (): (string | undefined)[] =>
    Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('fieldset:first-of-type label'),
    ).map((label) => label.textContent?.trim());

  const click = (input: HTMLInputElement): void => {
    input.click();
    fixture.detectChanges();
  };

  beforeEach(() => {
    emitted = [];
    fixture = TestBed.createComponent(BaselineControlsComponent);
    fixture.componentRef.setInput('windowDays', 30);
    fixture.componentRef.setInput('availableLocations', ['Site A', 'Site B']);
    fixture.componentRef.setInput('selectedLocations', ['Site A', 'Site B']);
    fixture.componentInstance.paramsChange.subscribe((params) => emitted.push(params));
    fixture.detectChanges();
  });

  it('renders the three windows and a checkbox per available location', () => {
    expect(windowLabels()).toEqual(['30 days', '60 days', '90 days']);
    expect(radios().map((radio) => radio.checked)).toEqual([true, false, false]);
    expect(checkboxes().length).toBe(2);
    expect(checkboxes().every((box) => box.checked)).toBe(true);
  });

  it('emits the selected window without touching the location filter', () => {
    const sixty = radios()[1];
    expect(sixty).toBeDefined();
    if (sixty === undefined) {
      return;
    }

    click(sixty);

    expect(emitted).toEqual([{ windowDays: 60, locations: [] }]);
  });

  it('emits the remaining subset when a location is unticked', () => {
    const siteA = checkboxes()[0];
    expect(siteA).toBeDefined();
    if (siteA === undefined) {
      return;
    }

    click(siteA);

    expect(emitted).toEqual([{ windowDays: 30, locations: ['Site B'] }]);
  });

  it('restores every tick when the last one is removed, since no filter means all', () => {
    for (const box of checkboxes()) {
      click(box);
    }

    expect(checkboxes().every((box) => box.checked)).toBe(true);
    expect(emitted.at(-1)).toEqual({ windowDays: 30, locations: [] });
  });
});
