import { HttpErrorResponse } from '@angular/common/http';
import { ValidationProblemDetails } from './problem-details.model';

export class AppError extends Error {
  constructor(
    public readonly status: number,
    public readonly title: string,
    public readonly detail: string | null,
    public readonly validationErrors: Record<string, string[]>,
    public readonly friendlyMessage: string,
    public readonly rawError: HttpErrorResponse
  ) {
    super(friendlyMessage);
    this.name = 'AppError';
  }

  hasFieldError(field: string): boolean {
    return !!this.validationErrors[field] && this.validationErrors[field].length > 0;
  }

  getFieldErrors(field: string): string[] {
    return this.validationErrors[field] || [];
  }
}
