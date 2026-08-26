# Day 14 — Piece 2: Signal Forms Preview Execution Log

## Verification Environment
- **Framework**: Angular 22 (Zoneless, Standalone Components, Signal Forms Preview)
- **API Target**: ASP.NET Core QuotesApi (`POST /api/quotes/` -> `http://localhost:5000/api/quotes/`)
- **Database**: SQLite `quotes.db`

---

## State Transition & Edge Case Logs

### Test 1: Pristine / Untouched Initial State
- **Input State**: `author = ''`, `text = ''`
- **Signal States**:
  - `authorTouched()` -> `false`
  - `textTouched()` -> `false`
  - `isDirty()` -> `false`
  - `isPristine()` -> `true`
  - `isValid()` -> `false`
- **Observed Behavior**:
  - Inputs are clean with placeholder text.
  - No validation error feedback displayed in DOM.
  - Submit button enabled; Reset button disabled.
- **Result**: ✅ PASS

---

### Test 2: Dirty State Before Blur (Typing)
- **Action**: User types `"Seneca"` into `#author-input`.
- **Signal States**:
  - `author()` -> `"Seneca"`
  - `authorDirty()` -> `true`
  - `isDirty()` -> `true`
  - `isPristine()` -> `false`
  - `authorTouched()` -> `false`
- **Observed Behavior**:
  - Author character count dynamically displays `6/100`.
  - Signal inspector badge toggles from `Pristine` to `Dirty`.
  - Reset button enables.
  - No premature error messages shown while typing.
- **Result**: ✅ PASS

---

### Test 3: Touched & Validation Error Trigger (Blur on Empty)
- **Action**: User focuses `#quote-text-input` and immediately tabs out without entering text.
- **Signal States**:
  - `textTouched()` -> `true`
  - `textErrors()` -> `{ required: true }`
  - `isTextInvalid()` -> `true`
- **Observed Behavior**:
  - `#quote-text-input` receives red error styling (`border-color: #ef4444`).
  - `aria-invalid="true"` set on textarea.
  - `<div id="text-error" role="alert">Quote text is required.</div>` renders in DOM.
- **Result**: ✅ PASS

---

### Test 4: Length Boundary Conditions
- **Case 4A (minlength rejection)**:
  - User enters `"Short"` (5 characters) in Quote Text.
  - `textErrors()` -> `{ minlength: { requiredLength: 10, actualLength: 5 } }`
  - Inline error displays: `"Must be at least 10 characters (currently 5)."`
- **Case 4B (maxlength rejection)**:
  - User inputs 105 characters in Author Name.
  - `authorErrors()` -> `{ maxlength: { requiredLength: 100, actualLength: 105 } }`
  - Counter changes to red `105/100`.
- **Result**: ✅ PASS

---

### Test 5: Clean Submission (`POST /api/quotes/`)
- **Action**:
  - Author: `"Marcus Aurelius"`
  - Text: `"Waste no more time arguing what a good man should be. Be one."`
  - User clicks **Publish Quote**.
- **Signal States & Network**:
  - `formState()` -> `'submitting'` -> `'success'`
  - HTTP Request: `POST http://localhost:5000/api/quotes/`
  - HTTP Payload: `{ "author": "Marcus Aurelius", "text": "Waste no more time arguing what a good man should be. Be one." }`
  - HTTP Response: `201 Created` with JSON:
    ```json
    {
      "id": 14,
      "author": "Marcus Aurelius",
      "authorId": null,
      "authorEntity": null,
      "text": "Waste no more time arguing what a good man should be. Be one."
    }
    ```
- **Observed Behavior**:
  - Success banner mounts with `role="alert"`.
  - Assigned ID #14 rendered.
  - Form fields cleanly reset (`author = ''`, `text = ''`, `touched = false`).
  - New quote instantly prepended to top of Live Feed list.
- **Result**: ✅ PASS

---

### Test 6: Failed Submission Handling
- **Action**: Disconnected backend server to test error resilience.
- **Signal States**:
  - `formState()` -> `'error'`
  - `serverError()` -> `"Network error — check your connection and backend server."`
- **Observed Behavior**:
  - Error banner displays with warning icon and `role="alert"`.
  - User's entered text remains preserved in input fields so data is not lost.
  - Submit button re-enables for retry.
- **Result**: ✅ PASS

---

### Test 7: Accessibility & Programmatic Focus
- **Action**: Submitted blank form.
- **Observed Behavior**:
  - `markAllAsTouched()` executed.
  - Both Author and Quote Text highlighted as invalid.
  - Focus immediately placed on `#author-input`.
- **Result**: ✅ PASS
