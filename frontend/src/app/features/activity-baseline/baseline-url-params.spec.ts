import { convertToParamMap, Params } from '@angular/router';

import {
  FallbackWindowDays,
  readBaselineState,
  UrlBaselineState,
  writeBaselineState,
} from './baseline-url-params';

describe('baseline URL parameters', () => {
  /**
   * Values are stringified on the way in because a query string has no other type — reading a
   * literal `60` here would test something the browser can never hand us.
   */
  const read = (params: Params): UrlBaselineState => {
    const asWritten = Object.entries(params).map(([key, value]) => [key, String(value)]);

    return readBaselineState(convertToParamMap(Object.fromEntries(asWritten)));
  };

  describe('window', () => {
    it.each([30, 60, 90])('accepts the offered window %s', (windowDays) => {
      expect(read({ window: String(windowDays) }).windowDays).toBe(windowDays);
    });

    it('falls back to 30 days when the window is absent', () => {
      expect(read({}).windowDays).toBe(30);
    });

    // PRD FR22 and the task's own success criterion: 13 is the canonical malformed value.
    it.each(['13', '0', '-30', '3000', 'abc', '', '30.0', '3e1', ' 30'])(
      'rejects %p and falls back to the documented default',
      (raw) => {
        expect(read({ window: raw }).windowDays).toBe(FallbackWindowDays);
      },
    );

    it('documents 30 days as the fallback', () => {
      expect(FallbackWindowDays).toBe(30);
    });
  });

  describe('locations', () => {
    it('reads a comma-separated list in the order the URL gave it', () => {
      expect(read({ locations: 'Site O,Site F' }).requestedLocations).toEqual(['Site O', 'Site F']);
    });

    it('trims names and drops blank entries', () => {
      expect(read({ locations: ' Site O , ,Site F,' }).requestedLocations).toEqual([
        'Site O',
        'Site F',
      ]);
    });

    it('keeps the first of a repeated name', () => {
      expect(read({ locations: 'Site O,Site O' }).requestedLocations).toEqual(['Site O']);
    });

    it('reads no filter when the parameter is absent or empty', () => {
      expect(read({}).requestedLocations).toEqual([]);
      expect(read({ locations: '' }).requestedLocations).toEqual([]);
    });

    /**
     * Unknown names are *not* rejected here — this function cannot know the account's locations.
     * The container drops them after the first response; see the page component's tests.
     */
    it('passes an unrecognised name through for the container to intersect', () => {
      expect(read({ locations: 'Nowhere' }).requestedLocations).toEqual(['Nowhere']);
    });
  });

  describe('writing', () => {
    it('writes the window and omits an empty filter', () => {
      expect(writeBaselineState({ windowDays: 60, locations: [] })).toEqual({ window: 60 });
    });

    it('writes a subset as a comma-separated list', () => {
      expect(writeBaselineState({ windowDays: 90, locations: ['Site O', 'Site F'] })).toEqual({
        window: 90,
        locations: 'Site O,Site F',
      });
    });

    it('round-trips a filtered selection back to the same state', () => {
      const written = writeBaselineState({ windowDays: 90, locations: ['Site O', 'Site F'] });

      expect(read(written)).toEqual({
        windowDays: 90,
        requestedLocations: ['Site O', 'Site F'],
      });
    });
  });
});
