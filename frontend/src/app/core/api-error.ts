/**
 * A transport or server failure, already turned into something a user can read. The interceptor
 * produces it so that no component or service has to know `HttpErrorResponse` or the shape of a
 * `ProblemDetails` body.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}
