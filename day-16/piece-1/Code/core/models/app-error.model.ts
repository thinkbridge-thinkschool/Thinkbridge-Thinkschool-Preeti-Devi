import { HttpErrorResponse } from '@angular/common/http';

export interface ValidationProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export class ApiProblemError extends Error {
  constructor(
    public readonly status: number,
    public readonly title: string,
    public readonly detail: string | null,
    public readonly validationErrors: Record<string, string[]>,
    public readonly userMessage: string,
    public readonly rawError: HttpErrorResponse
  ) {
    super(userMessage);
    this.name = 'ApiProblemError';
  }
}
