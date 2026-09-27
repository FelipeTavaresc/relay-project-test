import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renders the shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const shell = fixture.nativeElement as HTMLElement;
    expect(shell.querySelector('.brand')?.textContent).toContain('Relay');
    expect(shell.querySelector('main')).not.toBeNull();
  });
});
