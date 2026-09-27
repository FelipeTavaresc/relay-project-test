import { ParamMap, Params } from '@angular/router';

import { BaselineParams, DefaultWindowDays, WindowDays, WindowOptions } from './activity-baseline.models';

/**
 * The query string is the store of record (PRD FR21), which makes it user input: everything it
 * carries is validated here and nowhere else, and a malformed value falls back to a documented
 * default rather than reaching the API (FR22, `angular.md` § URL and persistence).
 *
 * Read and write live in one file on purpose — they are inverses, and separating a codec's two
 * halves would let them drift apart.
 *
 * Note the parameter names differ from the API's: the URL says `window` because that is what the
 * user is choosing, while the endpoint's parameter is `windowDays`. `ActivityBaselineService` owns
 * that translation.
 */
export const WindowParam = 'window';

export const LocationsParam = 'locations';

/**
 * What the URL asked for. `locations` is **unverified** — the names are whatever was typed into the
 * address bar, so the container intersects them against the names the API returned before any of
 * them is forwarded (FR20, task 7.3).
 */
export interface UrlBaselineState {
  readonly windowDays: WindowDays;
  readonly requestedLocations: readonly string[];
}

/**
 * The documented fallback (FR22). Named rather than inlined so the README and the tests can both
 * point at the same constant.
 */
export const FallbackWindowDays: WindowDays = DefaultWindowDays;

export function readBaselineState(params: ParamMap): UrlBaselineState {
  return {
    windowDays: readWindowDays(params.get(WindowParam)),
    requestedLocations: readLocations(params.get(LocationsParam)),
  };
}

/**
 * Query parameters for `Router.navigate`. Keys are omitted rather than set to an empty string, so
 * the default view's URL stays clean and "every location" is an absent parameter in the URL exactly
 * as it is an absent parameter in the request.
 */
export function writeBaselineState(params: BaselineParams): Params {
  const query: Params = { [WindowParam]: params.windowDays };

  if (params.locations.length > 0) {
    query[LocationsParam] = params.locations.join(',');
  }

  return query;
}

/**
 * Matched against the canonical spelling of each option rather than parsed as a number: `13` is
 * rejected, but so are `30.0`, `0x1e` and `3e1`, none of which a link this app produced would ever
 * contain. Rejecting rather than coercing is the rule (`csharp-dotnet.md` and `angular.md` agree).
 */
function readWindowDays(raw: string | null): WindowDays {
  return WindowOptions.find((option) => String(option) === raw) ?? FallbackWindowDays;
}

/** Comma-separated, trimmed, blanks dropped, first occurrence wins — order is the URL's. */
function readLocations(raw: string | null): readonly string[] {
  if (raw === null) {
    return [];
  }

  const names = raw
    .split(',')
    .map((name) => name.trim())
    .filter((name) => name.length > 0);

  return [...new Set(names)];
}
