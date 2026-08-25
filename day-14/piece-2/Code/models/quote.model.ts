export interface Quote {
  id: number;
  author: string;
  authorId: number | null;
  authorEntity: { id: number; name: string } | null;
  text: string;
}

export interface CreateQuoteRequest {
  author: string;
  text: string;
}

export interface QuoteFormValidation {
  required?: boolean;
  minlength?: { requiredLength: number; actualLength: number };
  maxlength?: { requiredLength: number; actualLength: number };
}
