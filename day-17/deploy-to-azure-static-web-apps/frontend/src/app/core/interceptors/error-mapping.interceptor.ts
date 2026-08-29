import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiProblemError, ValidationProblemDetails } from '../models/app-error.model';

export const errorMappingInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let title = 'Server Error';
      let detail: string | null = null;
      let validationErrors: Record<string, string[]> = {};
      let userMessage = 'An unexpected server error occurred.';

      // status 0 must be checked before the object-shape check below: a
      // transport-level failure (offline, DNS, CORS, server not running)
      // sets error.error to a ProgressEvent/ErrorEvent, which is itself an
      // object — so `typeof error.error === 'object'` is true and would
      // otherwise swallow this case into the generic "Server Error" branch
      // before ever reaching a status === 0 check.
      if (error.status === 0) {
        title = 'Network Error';
        userMessage = 'Unable to reach the backend server. Check your connection, or that the server is running.';
      } else if (error.error && typeof error.error === 'object') {
        const problem = error.error as ValidationProblemDetails;
        title = problem.title || (error.status === 404 ? 'Not Found' : 'Error');
        detail = problem.detail || null;

        if (problem.errors && typeof problem.errors === 'object') {
          validationErrors = problem.errors;
          const messages = Object.entries(problem.errors)
            .map(([field, errs]) => `${field}: ${errs.join(', ')}`)
            .join(' | ');
          userMessage = messages || title;
        } else if (problem.detail) {
          userMessage = problem.detail;
        } else if (problem.title) {
          userMessage = problem.title;
        }
      } else if (error.status === 404) {
        title = 'Not Found';
        userMessage = 'The requested resource was not found.';
      }

      const apiProblemError = new ApiProblemError(
        error.status,
        title,
        detail,
        validationErrors,
        userMessage,
        error
      );

      return throwError(() => apiProblemError);
    })
  );
};
