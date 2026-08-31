# Observed run — 31 August 2026

Captured against `QuoteRelay.Api` running in the Development profile
(`Ceiling: 8`, `MaxQuotesPerDigest: 5`, `RenderSliceDelay: 750 ms`) on
`http://localhost:5080`. Timings come from `curl -w "%{time_total}"`; the
`handOffMilliseconds` figure is measured inside the endpoint itself.

## 1. The request path does not wait for the work

Three submissions, back to back. The first asks for five quotes, which is
5 × 750 ms ≈ 3.75 s of rendering.

```
--- A: healthy, 5 quotes (5 x 750ms of work) ---
{"assignmentId":"d5348ddd-...","backlog":0,"ceiling":8,"handOffMilliseconds":4.552} | http=202 total=0.069263s
--- B: doomed, quote 4242 is not in the catalogue ---
{"assignmentId":"12cb9eaf-...","backlog":1,"ceiling":8,"handOffMilliseconds":0.105} | http=202 total=0.003305s
--- C: healthy, submitted while A is still rendering ---
{"assignmentId":"0a161b47-...","backlog":2,"ceiling":8,"handOffMilliseconds":0.029} | http=202 total=0.002909s
```

Read the two numbers together. `handOffMilliseconds` — validate, build the
assignment, write to the channel — settles at **0.03 ms** once the JIT has
warmed. The work that assignment represents takes **3,793 ms**. That ratio is
the whole point of the exercise.

Note also that A was still rendering when B and C arrived: `backlog` climbs to
2 while the pump is busy, and the API keeps answering in microseconds.

Vitals taken immediately after the three POSTs, with nothing finished yet:

```
{"backlog":2,"ceiling":8,"intakeSealed":false,"handled":0,"abandoned":0}
```

## 2. Malformed input is still rejected synchronously

Deferring the work does not mean deferring the answer. Shape problems are
caught on the request thread and reported as 400, not as a 202 followed by
silence.

```
{"type":"...rfc9110#section-15.5.1","title":"One or more validation errors occurred.",
 "status":400,"errors":{"Subscriber":["A subscriber address must contain an '@'."]}} | http=400
```

## 3. One failed assignment does not stop the next

`GET /relay/digests` after the queue drained. B asked for quote 4242, which is
a well-formed id absent from the catalogue — so it passes validation and can
only fail once the work is already off the request thread.

```
Delivered  third@example.test     Assembled in 1524 ms.
Faulted    doomed@example.test    Quote 4242 is absent from the catalogue, so assignment 12cb9eaf-... cannot be assembled.
Delivered  first@example.test     Assembled in 3793 ms.
```

Submission order was A, B, C. B faulted between them and C was still
delivered, on the same pump, without a restart. Vitals confirm all three were
accounted for:

```
{"backlog":0,"ceiling":8,"intakeSealed":false,"handled":3,"abandoned":0}
```

The failure is recorded with its reason rather than swallowed; it is also
logged at `Error` with event id 1803 and the exception attached.

## 4. Finished work is collectable afterwards

`GET /relay/digests/{id}` for assignment C:

```
Delivered
Quote digest for third@example.test
Assignment 0a161b47-7d3a-4378-a236-db7f44be7bee
Requested 2026-08-31 05:32:26Z

1. "Premature optimisation is the root of all evil." — Donald Knuth
2. "Deleted code is debugged code." — Jeff Sickel
```

## 5. Pump lifecycle in the log stream

Startup, ordinary processing, and the gate opening:

```
info: QuoteRelay.Api.Relay.RelayPumpService[1800]
      Relay pump online; queue ceiling 32.
info: QuoteRelay.Api.Relay.RelayGateSentinel[1810]
      Relay gate open: intake accepting up to 32 queued assignment(s).
info: QuoteRelay.Api.Relay.RelayPumpService[1801]
      Assignment bc5c7c99-... picked up for midflight@example.test (5 quotes); backlog now 0.
info: QuoteRelay.Api.Relay.RelayPumpService[1802]
      Assignment bc5c7c99-... delivered in 2039 ms.
```

## 6. Graceful shutdown

Shutdown is verified by the test suite rather than by a captured console
transcript. `RelayShutdownTests` resolves the real `IHost` from the running
application and calls `StopAsync()` — the same call the console lifetime makes
when you press Ctrl+C — then asserts that the intake ends up sealed, that the
assignment interrupted mid-render is stamped `Abandoned` rather than reported
as delivered, and that `ExecuteTask` finished in `RanToCompletion` rather than
faulted or still running.

To watch it happen in a terminal instead, run `dotnet run --project
src/QuoteRelay.Api`, submit a five-quote digest, and press Ctrl+C while it is
rendering. Expect log events 1811 (gate closing), 1804 (assignment abandoned),
1805 (pump stopped waiting) and 1807 (pump offline, with counts).
