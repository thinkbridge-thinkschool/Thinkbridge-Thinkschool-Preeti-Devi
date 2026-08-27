import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { retry, timer } from 'rxjs';

export const retryGetInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.method !== 'GET' && req.method !== 'HEAD') {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: 2,
      delay: (error: HttpErrorResponse, retryCount: number) => {
        const isTransient = error.status === 0 || (error.status >= 500 && error.status <= 504);
        if (!isTransient) {
          throw error;
        }
        return timer(retryCount * 200);
      },
    })
  );
};
