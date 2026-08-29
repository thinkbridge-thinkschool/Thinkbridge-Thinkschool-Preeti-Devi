# Evidence

| File | What it shows |
|---|---|
| `managed-identity-verification.txt` | The managed-identity proof: architecture, the decoded token's safe metadata (issuer, audience, roles, subject — never the raw token), the live create/read/delete round-trip through the proxy, the regression check that the human login flow still works, and the zero-secret audit. |
| `verification-run.txt` | Output of `../scripts/verify.sh` against the live deployment, 2026-08-29. 19 checks: reachability, Static Web Apps host headers, CORS, authorization, and the managed-identity round-trip. |
| `curl-transcripts.txt` | Raw request/response transcripts against the live API: health, the empty state, CORS before and after the fix, login, and the 401/403 paths. The credentials in it are the demo login the app displays on its own sign-in screen. |
| `01-empty-state.jpg` … `05-signed-out-delete-hidden-hint.jpg` | The app running live, in order: empty state, a 401 on an unauthenticated create, an authenticated create, an authenticated delete, and the delete control hidden when signed out. |
| `lighthouse-summary.txt` | The three Lighthouse rounds in narrative: what each run scored, which fix was applied between rounds, and why the final performance number is 92 rather than ≥95. Includes the experiment that measured worse and was reverted. |
| `lighthouse-final.report.html` / `.json` | The final Lighthouse run against the live Static Web App (mobile preset). Open the HTML in a browser. |
| `lighthouse-report.report.html` / `.json` | Run 1, the initial deploy — the baseline the summary's fixes are measured against. |
| `lighthouse-report-desktop.report.html` / `.json` | The `--preset=desktop` cross-check, which rules out mobile-CPU throttling as the cause of the performance ceiling. |

Two further write-ups sit one level up, next to the code they describe:
[`../verification-log.md`](../verification-log.md) (the round-by-round log, including the
three bugs found) and [`../agent-output.md`](../agent-output.md) (the build summary).

## Reading `verification-run.txt`

18 of the 19 checks pass. The failure is `index.html not cached`: Static Web Apps consumes
`frontend/public/staticwebapp.config.json` at deploy time rather than serving it, and the
currently-live deployment predates the `routes[]` cache rules in the restored file, so the
site still returns SWA's default `public, must-revalidate, max-age=30`. It clears on the
next `./scripts/deploy.sh frontend`.
