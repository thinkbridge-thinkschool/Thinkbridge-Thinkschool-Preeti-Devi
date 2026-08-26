# Day 15 — Interceptors & Error Mapping Verification Log

## Real API Grounding
- **Backend API**: ASP.NET Core Minimal API (Week-1 Quotes Backend)
- **Primary Endpoint**: `GET /api/quotes?page={page}&size={size}`
- **Response Shape (200 OK)**:
  ```json
  [
    {
      "id": 1,
      "author": "Marcus Aurelius",
      "text": "Waste no more time arguing what a good man should be. Be one."
    },
    {
      "id": 2,
      "author": "Seneca",
      "text": "We suffer more often in imagination than in reality."
    }
  ]
  ```
- **Error Response (400 Bad Request with RFC 7807/9457 ValidationProblemDetails)**:
  - Query: `GET /api/quotes?page=0&size=500`
  - Raw JSON:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "errors": {
      "page": ["Page must be at least 1."],
      "size": ["Size must be between 1 and 100."]
    }
  }
  ```

---

## States & Edges Exercised

| State / Edge | Trigger / Test Condition | Expected Behavior | Verification Status |
| :--- | :--- | :--- | :--- |
| **Loading State** | Initial fetch or page transition | `state === 'loading'`, spinner rendered, screen reader `aria-live="polite"` active | **PASSED** (Characterization + UI) |
| **Populated Success** | `GET /api/quotes?page=1&size=5` (200 OK) | Quotes rendered in card grid with `#id`, `text`, `author`; pagination updated | **PASSED** (Characterization + UI) |
| **Empty State** | `GET /api/quotes?page=99&size=10` (200 OK with `[]`) | `state === 'empty'`, display "No Quotes Found" with reset CTA | **PASSED** (Characterization + UI) |
| **400 Validation Error** | `GET /api/quotes?page=0&size=500` (400) | `errorMappingInterceptor` extracts `errors.page` and `errors.size`, formats userMessage: *"Page must be at least 1. Size must be between 1 and 100."* | **PASSED** (Characterization + UI) |
| **404 Not Found** | `GET /api/quotes/999` (404 ProblemDetails) | Extracted `detail`: *"Quote with ID 999 does not exist in the database."* | **PASSED** (Characterization Test) |
| **Auth Injection** | Active token in `AuthTokenService` | `Authorization: Bearer <token>` attached automatically to outgoing requests | **PASSED** (Interceptor Test) |
| **Idempotent Retry** | `GET /api/quotes` returning 503 | Retries up to 2 times with backoff (`retryCount * 200ms`) before failing | **PASSED** (Interceptor Test) |
| **Non-Idempotent Protection** | `POST /api/quotes` returning 500 | **Zero retries** executed; fails immediately to avoid duplicate quote creation | **PASSED** (Interceptor Test) |
| **Client Error Non-Retry** | `GET /api/quotes?page=0` returning 400 | Interceptor recognizes 4xx and throws immediately without wasting retry attempts | **PASSED** (Interceptor Test) |

---

## Concrete Bug Caught & Remediation
- **Bug Caught in Agent PR**: The agent initially assumed `error.error.message` or a flat string array `error.error.errors` array (`string[]`). In ASP.NET Core `Results.ValidationProblem(errors)`, the error structure is a key-value dictionary `Record<string, string[]>`. Because of this mismatch, `userMessage` collapsed to `undefined` or generic fallback, hiding the actual server validation error. Furthermore, the agent had configured a blanket `retry(2)` on all requests, which would resend non-idempotent `POST` requests if a network timeout occurred.
- **Fix Directed & Verified**:
  1. Updated `errorMappingInterceptor` to iterate `Object.keys(errors)` and flatten all `string[]` validation arrays into human-readable sentences.
  2. Restricted `retryGetInterceptor` to check `req.method === 'GET' || req.method === 'HEAD'` and explicitly reject `error.status >= 400 && error.status < 500`.

---

## Contract Break Analysis
If the backend changes:
1. **Contract Change**: Moving from ASP.NET Core `ValidationProblemDetails` (`errors: { [key: string]: string[] }`) to a custom format `{ error: string }`.
   - **What breaks**: The characterization test `Contract 2: 4xx ValidationProblemDetails` fails immediately at compile/test time. In runtime, `errorMappingInterceptor` falls back to HTTP status message until updated, preventing UI crashes.
2. **Contract Change**: Altering query parameter names from `page` & `size` to `pageNumber` & `pageSize`.
   - **What breaks**: `GET /api/quotes?page=1&size=10` characterization test fails on `httpMock.expectOne()`. Backend defaults to page 1 size 10 regardless of UI inputs.
