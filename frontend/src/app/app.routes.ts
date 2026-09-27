import { Routes } from '@angular/router';

/**
 * One feature, lazy-loaded with `loadComponent` so the initial bundle stays the shell
 * (`angular.md` § Project layout). The baseline view is the landing view — the dashboard is the
 * whole application.
 */
export const routes: Routes = [
  {
    path: '',
    title: 'Activity baseline — Relay',
    loadComponent: () =>
      import('./features/activity-baseline/activity-baseline-page.component').then(
        (m) => m.ActivityBaselinePageComponent,
      ),
  },
  { path: '**', redirectTo: '' },
];
