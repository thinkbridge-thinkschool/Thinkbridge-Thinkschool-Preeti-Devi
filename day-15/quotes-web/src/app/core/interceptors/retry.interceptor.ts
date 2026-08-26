import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { retry, timer } from 'rxjs';

export const retryGetInterceptor: HttpInterceptorFn = (req, next) => {
  const isIdempotent = req.method === 'GET' || req.method === 'HEAD';

  if (!isIdempotent) {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: 2,
      delay: (error: unknown, retryCount: number) => {
        if (error instanceof HttpErrorResponse) {
          // Never retry deterministic client errors (4xx)
          if (error.status >= 400 && error.status < 500) {
            throw error;
          }
          // Retry transient network drop (0) or 5xx server errors
          if (error.status === 0 || error.status >= 500) {
            const backoffMs = retryCount * 200;
            return timer(backoffMs);
          }
        }
        throw error;
      },
    })
  );
};
