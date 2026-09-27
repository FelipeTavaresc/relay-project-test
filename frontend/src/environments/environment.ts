/**
 * Build-time configuration. One file, not a dev/prod pair: the API is reached at the same
 * published port in both cases (`dotnet run` locally, the port docker compose publishes for the
 * `api` service), so a second near-identical file would be ceremony rather than configuration.
 * A different deployment overrides it with an `angular.json` fileReplacement.
 */
export const environment = {
  /** Origin of the .NET API. No path: the endpoint lives in ActivityBaselineService, nowhere else. */
  apiBaseUrl: 'http://localhost:5141',

  /**
   * The single account this dashboard shows. Authentication and multi-account switching are both
   * out of scope (PRD § Out of Scope), so the account is configuration rather than user input.
   * Account 6 (Metro Collision Centers, 15 locations) is the PRD's reference multi-location case.
   */
  accountId: 6,
} as const;
