import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiProblemError, ValidationProblemDetails } from './app-error.model';

export const errorMappingInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const problem = (error.error ?? {}) as ValidationProblemDetails;
        const status = error.status || problem.status || 500;
        const title = problem.title || getDefaultTitleForStatus(status);
        const detail = problem.detail || null;
        const validationErrors: Record<string, string[]> = problem.errors || {};

        const userMessage = extractFriendlyMessage(status, title, detail, validationErrors);

        const appError = new ApiProblemError(
          status,
          title,
          detail,
          validationErrors,
          userMessage,
          error
        );

        return throwError(() => appError);
      }

      return throwError(() => error);
    })
  );
};

function extractFriendlyMessage(
  status: number,
  title: string,
  detail: string | null,
  errors: Record<string, string[]>
): string {
  const errorKeys = Object.keys(errors);
  if (errorKeys.length > 0) {
    const errorMessages: string[] = [];
    for (const key of errorKeys) {
      const fieldErrors = errors[key];
      if (Array.isArray(fieldErrors) && fieldErrors.length > 0) {
        errorMessages.push(...fieldErrors);
      }
    }
    if (errorMessages.length > 0) {
      return errorMessages.join(' ');
    }
  }
  if (detail && detail.trim().length > 0) {
    return detail;
  }
  switch (status) {
    case 400:
      return title || 'Invalid request. Please check your inputs.';
    case 401:
      return 'You are not authenticated. Please log in to continue.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 404:
      return 'The requested resource could not be found.';
    case 409:
      return 'A conflict occurred while processing your request.';
    case 422:
      return 'The request could not be processed due to validation errors.';
    case 500:
    case 502:
    case 503:
    case 504:
      return 'The server encountered an issue. Please try again shortly.';
    default:
      return title || 'An unexpected error occurred. Please try again.';
  }
}

function getDefaultTitleForStatus(status: number): string {
  switch (status) {
    case 400:
      return 'Bad Request';
    case 401:
      return 'Unauthorized';
    case 403:
      return 'Forbidden';
    case 404:
      return 'Not Found';
    case 422:
      return 'Unprocessable Entity';
    case 500:
      return 'Internal Server Error';
    case 503:
      return 'Service Unavailable';
    default:
      return 'HTTP Error';
  }
}
