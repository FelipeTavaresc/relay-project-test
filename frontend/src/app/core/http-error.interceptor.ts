import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { ApiError } from './api-error';

/** The subset of RFC 7807 `ProblemDetails` the API fills in (see Relay.Api ProblemDetails usage). */
interface ProblemDetails {
  readonly title?: string;
  readonly detail?: string;
}

const UnreachableMessage =
  'Could not reach the API. Check that it is running and try again.';

/**
 * The one place `HttpErrorResponse` is unwrapped. Everything downstream sees an {@link ApiError}
 * with a message worth showing, so the feature's error state is about *what failed*, not about
 * HTTP plumbing (`angular.md` § Data fetching).
 */
export const httpErrorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(catchError((error: unknown) => throwError(() => toApiError(error))));

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (!(error instanceof HttpErrorResponse)) {
    return new ApiError(0, UnreachableMessage);
  }

  // Status 0 is the browser refusing to report why — DNS, a dead port, or a blocked CORS preflight.
  if (error.status === 0) {
    return new ApiError(0, UnreachableMessage);
  }

  return new ApiError(error.status, describe(error));
}

function describe(error: HttpErrorResponse): string {
  const problem = asProblemDetails(error.error);

  if (problem?.detail !== undefined && problem.detail.length > 0) {
    return problem.detail;
  }

  if (problem?.title !== undefined && problem.title.length > 0) {
    return problem.title;
  }

  return `The API responded with ${error.status} ${error.statusText}.`;
}

/**
 * A type guard rather than a cast: the body is whatever the server sent, and a 502 from a proxy
 * is an HTML page, not a problem document.
 */
function asProblemDetails(body: unknown): ProblemDetails | null {
  if (typeof body !== 'object' || body === null) {
    return null;
  }

  const candidate = body as { title?: unknown; detail?: unknown };
  const title = typeof candidate.title === 'string' ? candidate.title : undefined;
  const detail = typeof candidate.detail === 'string' ? candidate.detail : undefined;

  return title === undefined && detail === undefined ? null : { title, detail };
}
