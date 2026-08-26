# Day 14 — Piece 2 Evidence Directory

This directory contains test verification logs, execution traces, and visual evidence for the **Signal Forms Preview** implementation connecting to our real Week-1 backend endpoint (`POST /api/quotes/`).

## Verification & Execution Files
- [`Execution_Log.md`](file:///c:/Users/abhinav/thinkschool/Thinkbridge-Thinkschool-Preeti-Devi/day-14/piece-2/Evidence/Execution_Log.md): Comprehensive test log covering all signal states:
  1. Pristine / Untouched state
  2. Dirty state while typing (live character counter updates)
  3. Touched & Validation Error Trigger (blur on empty)
  4. Length Boundary Conditions (minlength 10 chars, maxlength 100/500 chars)
  5. Clean Submission (`POST /api/quotes/` -> 201 Created)
  6. Failed Submission Handling (500/400/Network error alerts)
  7. Accessibility & Programmatic Focus Management

## Visual Evidence Assets
- `01-empty-state.png`: Initial pristine form state with clean signal inspector indicators.
- `02-validation-errors.png`: Field-level validation error states and accessibility alerts.
- `03-server-error.png`: Server-side error banner on HTTP rejection.
- `04-axe-accessibility.png`: Accessibility audit verification (labels, ARIA, focus).
- `05-success-state.png`: 201 Created success banner and immediate live feed stream insertion.
