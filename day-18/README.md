# Day 18 — Background Jobs

An ASP.NET Core service that takes slow work off the HTTP request thread and
hands it to a single in-process consumer, plus the reasoning about when that is
the right tool and when it is not.

The worked example is a **quote digest relay**. A caller asks for a digest of
several quotes to be assembled for a subscriber. Assembling one costs a render
slice per quote, so a five-quote digest takes seconds — long enough that holding
the connection open for it would be indefensible. The API validates the request,
queues an assignment, and answers immediately; a background consumer does the
assembling and records what happened.

## 1. What was implemented

| Piece | Type | Role |
| --- | --- | --- |
| Job | `RelayAssignment` | Immutable record: assignment id, subscriber, quote ids, accepted-at |
| Queue (producer half) | `IRelayIntake` | `Offer` returns `Accepted`, `Saturated` or `Sealed`; never blocks |
| Queue (consumer half) | `IRelayOutlet` | `DrainAsync` yields assignments, parking asynchronously when empty |
| Queue implementation | `BoundedRelayChannel` | One bounded `Channel<RelayAssignment>` behind both halves |
| Worker | `RelayPumpService : BackgroundService` | Drains, scopes, runs, isolates failures, stops on cancellation |
| Lifecycle hook | `RelayGateSentinel : IHostedService` | Seals the intake the moment shutdown begins |
| The slow work | `DigestAssemblyProcessor : IAssignmentProcessor` | Scoped; renders the digest, cancellation-aware |
| Data source | `IQuoteCatalogue` / `SeededQuoteCatalogue` | Scoped, stands in for a DbContext |
| Observation | `IRelayLedger`, `IDigestShelf` | Per-assignment stage and note; finished digest bodies |
| API | Minimal API in `Program.cs` | Submit, poll one, list all, vitals |

Layout:

```
day-18/
  src/QuoteRelay.Api/        Contracts/  Digests/  Relay/  Program.cs
  tests/QuoteRelay.Tests/    31 tests across five suites, plus Support/
  scripts/relay-demo.ps1     End-to-end demo against a running instance
  evidence/observed-run.md   Captured output from a real run
```

Run it:

```
dotnet run --project src/QuoteRelay.Api          # listens on http://localhost:5080
pwsh scripts/relay-demo.ps1                      # in a second terminal
dotnet test                                      # 31 tests
```

## 2. Why the slow work left the request path

An HTTP request that waits for slow work costs more than the caller's patience.
The connection, the request-processing thread's state machine, the socket buffer
and any per-request middleware state all stay pinned for the duration. Under
concurrency that turns into queued connections and timeouts long before the CPU
is busy, because the bottleneck is occupancy, not compute. The caller is also
forced to hold a connection open across something they cannot influence, so a
transient failure two seconds in becomes their problem to retry.

Splitting the operation removes all of that. The request does the part that is
fast and genuinely needs to be synchronous — validating input, minting an id,
accepting responsibility — and returns `202 Accepted` with a URL to poll. The
part that is slow runs once, on one thread, at a pace the service controls.

The measured effect, from `evidence/observed-run.md`:

```
handOffMilliseconds: 0.029      (validate + build + enqueue)
Assembled in:        3793 ms    (the work that hand-off represents)
```

Three hundredths of a millisecond on the request thread, standing in for
nearly four seconds of work. Under load the queue depth grows instead of the
response time, and the ceiling turns overload into an explicit `503` rather
than a slow collapse.

## 3. Architecture and flow

```
POST /relay/digests
        │
        │  request thread
        ▼
   validate ──► 400 if malformed
        │
        ▼
   RelayAssignment ──► ledger.Stamp(Accepted)
        │
        ▼
   IRelayIntake.Offer ──► Saturated / Sealed ──► 503
        │  Accepted
        ▼
   ┌──────────────────────────────┐
   │  BoundedRelayChannel         │   bounded, single reader
   │  Channel<RelayAssignment>    │   many writers
   └──────────────────────────────┘
        │  202 Accepted returned here — nothing below has run yet
        │
        │  worker thread (pool)
        ▼
   IRelayOutlet.DrainAsync ── parks when empty, no polling
        │
        ▼
   RelayPumpService.HandleAsync
        │  ledger.Stamp(InProgress)
        │  using scope = scopeFactory.CreateScope()
        ▼
   IAssignmentProcessor (scoped) ── DigestAssemblyProcessor
        │        │
        │        └── IQuoteCatalogue (scoped)
        │
        ├── success ──► IDigestShelf.Stow  +  ledger.Stamp(Delivered)
        ├── throw   ──► log Error 1803     +  ledger.Stamp(Faulted)   → loop continues
        └── cancel  ──► log Warning 1804   +  ledger.Stamp(Abandoned) → loop unwinds

GET /relay/digests/{id}  reads ledger + shelf
GET /relay/vitals        reads backlog, ceiling, pump counters
```

The producer and the consumer never reference each other. They share one
channel instance, exposed through two interfaces so the API cannot dequeue and
the pump cannot enqueue — a compile-time guarantee rather than a convention.

## 4. How the queue works

`BoundedRelayChannel` wraps a single `Channel<RelayAssignment>` created with
`Channel.CreateBounded`, `SingleReader = true`, `SingleWriter = false` and
`FullMode = BoundedChannelFullMode.Wait`.

**Why bounded.** An unbounded queue is a memory leak with good manners: it
accepts work faster than it can be done and hides the imbalance until the
process dies. A ceiling makes the imbalance visible at the point where
something can still be done about it.

**Why `TryWrite` rather than `WriteAsync`.** `FullMode.Wait` combined with
`TryWrite` gives a non-blocking answer to "is there room?". At the ceiling
`TryWrite` returns `false`, `Offer` reports `Saturated`, and the endpoint sheds
load with a `503`. The alternatives are worse: `WriteAsync` would park the
request thread waiting for queue space, which is the exact stall the relay
exists to remove, and `DropOldest` or `DropWrite` would discard accepted work
without telling anyone.

**Why the consumer side is an `IAsyncEnumerable`.** `ReadAllAsync` parks on an
awaitable when the queue is empty. An idle pump therefore holds no thread and
runs no timer — there is no polling interval to tune and no wasted wake-ups.
The sequence also ends *normally* once the intake is sealed and drained, which
gives the worker a second, non-exceptional way to finish.

`Offer` returns a three-valued result rather than a boolean so the endpoint can
distinguish "too busy, try again" from "we are shutting down, do not try
again". Those deserve different client behaviour even though both are `503`.

## 5. How the BackgroundService works

`RelayPumpService.ExecuteAsync` is one loop:

```csharp
await foreach (var assignment in _outlet.DrainAsync(stoppingToken))
{
    await HandleAsync(assignment, stoppingToken);
}
```

Everything interesting is in how each assignment is handled.

**Dependency injection.** `AddHostedService` registers the pump as a singleton,
so it cannot hold a scoped dependency as a field — a captured `DbContext` would
live for the lifetime of the process and accumulate tracked entities forever.
The pump therefore holds `IServiceScopeFactory` and opens a fresh scope per
assignment, disposing it before the next one starts. That is the background
equivalent of a request scope, and it is load-bearing here: both
`DigestAssemblyProcessor` and `IQuoteCatalogue` are registered scoped, so
resolving the processor outside a scope throws. The test rig builds its provider
with `validateScopes: true` so a regression there fails the suite rather than
production.

The pump is also registered as a plain singleton and *then* handed to the host
via `AddHostedService(sp => sp.GetRequiredService<RelayPumpService>())`. That is
one instance with two registrations, which is what lets `/relay/vitals` read its
counters. Calling `AddHostedService<RelayPumpService>()` after
`AddSingleton<RelayPumpService>()` would create two.

**Concurrency.** One reader, one assignment at a time. That is a deliberate
choice, not a limitation of the design: it makes ordering observable and keeps
the load the background work puts on downstream systems predictable. Widening it
means starting several pumps against the same outlet, or bounding concurrency
with a `SemaphoreSlim` inside `HandleAsync`; the queue supports either.

**What the loop avoids.** No `async void`, so exceptions have somewhere to go.
No `Thread.Sleep`, `.Wait()` or `.Result`, so no thread is ever blocked. No
fire-and-forget: every task the service starts is awaited inside `ExecuteAsync`,
which means when `ExecuteAsync` finishes there is provably nothing left running.
And no privately created `CancellationTokenSource` standing between the host and
the work — the token the processor receives is the host's own.

## 6. How graceful shutdown works

The chain, from the outside in:

1. Something asks the application to stop — Ctrl+C, `SIGTERM`, IIS or a
   container orchestrator draining the pod. `ConsoleLifetime` (or the equivalent)
   calls `IHost.StopAsync`.
2. The host stops hosted services in **reverse registration order**.
   `RelayGateSentinel` is registered after the pump, so it stops first: it calls
   `IRelayIntake.Seal()`. From this instant `Offer` returns `Sealed`, and any
   request still in flight gets an honest `503` instead of queueing work that
   will never run.
3. The host calls `RelayPumpService.StopAsync`. The base
   `BackgroundService.StopAsync` cancels the `CancellationTokenSource` behind the
   `stoppingToken` it originally passed to `ExecuteAsync`, then awaits
   `ExecuteTask` — so the host does not proceed until the loop has genuinely
   ended, bounded by `HostOptions.ShutdownTimeout` (30 seconds by default).
4. `stoppingToken` is now cancelled, and it reaches the work by two routes.
   If the pump was parked in `ReadAllAsync`, that throws
   `OperationCanceledException` and the `await foreach` unwinds. If an assignment
   was mid-flight, the same token was passed to
   `processor.RunAsync(assignment, stoppingToken)` and from there to
   `Task.Delay(slice, cancellationToken)` and `LookupAsync`, so the innermost
   await throws instead of finishing its slice.
5. Either way the exception surfaces in `ExecuteAsync`, where the filtered catch
   `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)`
   treats it as an expected end rather than a fault. The pump logs its counts
   and `ExecuteAsync` returns; `ExecuteTask` completes as `RanToCompletion`.

A cancelled assignment is stamped `Abandoned`, not `Faulted` and certainly not
`Delivered`. The filter matters: an `OperationCanceledException` raised for any
*other* reason falls through to the general handler and is treated as a genuine
job failure, which is what it is.

The honest limitation: an in-process queue has nowhere to put the backlog. Work
still sitting in the channel at shutdown is gone, and the assignment that was
running is recorded as abandoned rather than resumed. That is acceptable for
work a caller can resubmit, and it is precisely the line past which durable job
storage becomes necessary — see section 8.

## 7. BackgroundService versus IHostedService

`IHostedService` is the lower-level contract: `StartAsync(CancellationToken)`
and `StopAsync(CancellationToken)`, called by the host as it starts and stops.
Both are expected to *return* reasonably promptly — the host awaits `StartAsync`
in sequence for every hosted service, so a `StartAsync` that never returns
prevents the application from starting at all. Implementing a long-running loop
directly on `IHostedService` therefore means starting a task in `StartAsync`,
storing it, creating and owning a `CancellationTokenSource`, cancelling it in
`StopAsync`, awaiting the task with a timeout, and making sure exceptions from it
are not lost.

`BackgroundService` is an abstract base class that implements `IHostedService`
and does all of that once, correctly. It gives you a single method,
`ExecuteAsync(CancellationToken stoppingToken)`, which is *allowed* to run for
the lifetime of the application: `StartAsync` invokes it and returns as soon as
it hits its first incomplete await, and `StopAsync` cancels the token and awaits
the task it kept.

This repository contains one of each, so the distinction is visible in the code
rather than only described:

- `RelayPumpService` extends `BackgroundService`. It is one continuous operation
  from startup to shutdown, it needs a cancellation token that trips on
  shutdown, and it needs its loop awaited during shutdown. Every one of those is
  something the base class already provides. Written against raw
  `IHostedService` it would be the same loop plus roughly thirty lines of
  lifecycle bookkeeping that could only differ from the framework's by being
  wrong.
- `RelayGateSentinel` implements `IHostedService` directly. It has no loop at
  all — it logs the ceiling on start and seals the intake on stop. There is
  nothing for `ExecuteAsync` to do, and `StopAsync` is genuinely where its work
  belongs. Forcing it into a `BackgroundService` would mean an `ExecuteAsync`
  that returns immediately and an override of `StopAsync` anyway.

The rule that falls out: reach for `IHostedService` when you want to *do
something at* start or stop, and for `BackgroundService` when you want to *keep
doing something between* them.

## 8. BackgroundService versus Hangfire

**Choose Hangfire over a hosted service when background work must be durable,
scheduled or recurring, retryable across restarts, and operationally
observable.**

A `BackgroundService` over a channel is the right choice when the queue belongs
to the application, the work is cheap to re-request, processing is continuous,
and no external system needs to see or manage the jobs. It costs one class and
no infrastructure; everything stays in-process, in memory, and inside a single
deployment. This relay qualifies on all four counts: a lost digest is a
resubmitted digest.

The moment any of those assumptions breaks, an in-process queue starts being
patched into a worse version of a job server. Hangfire — or Quartz.NET, or a
real broker — earns its cost when you need:

- **Durability.** Jobs survive process restarts, deployments and crashes because
  they live in SQL Server, PostgreSQL or Redis rather than in a `Channel<T>`.
  Section 6's limitation disappears.
- **Scheduling and delay.** `Schedule` and `Enqueue`-after-a-delay are
  first-class. Doing this over a channel means inventing a timer wheel and
  persisting it.
- **Recurrence.** Cron-style recurring jobs with a single registration, rather
  than a `PeriodicTimer` loop plus your own missed-run and overlap handling.
- **Retries with policy.** Automatic retry counts, backoff, and a failed-job
  state you can inspect and requeue. Our pump logs a failure and moves on; it
  has no notion of attempt two.
- **Continuity across restarts.** A job interrupted by a deployment is picked up
  again rather than abandoned.
- **Operational visibility.** A dashboard showing queued, processing, succeeded,
  failed and scheduled jobs, with stack traces and manual requeue. The ledger and
  `/relay/vitals` here are a deliberately small substitute that dies with the
  process.
- **Distribution.** Several instances drawing from one shared queue, with
  server-side locking so a job runs once. A channel is per-process by
  construction, so scaling out multiplies the queues instead of sharing one.

The two are not exclusive. A common shape is a hosted service for cheap,
high-volume, in-process fan-out and a durable job server for anything a customer
would notice the loss of.

## 9. What happens when a job fails

`HandleAsync` wraps the processor call in three handlers, ordered deliberately:

```csharp
catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
{
    // shutdown, not a defect: stamp Abandoned, then rethrow so the loop unwinds
}
catch (Exception ex)
{
    // a job defect: stamp Faulted with the message, log Error 1803, keep going
}
```

The general `catch` deliberately does not rethrow. That is the one place in the
service where swallowing an exception is correct — an unhandled exception
escaping `ExecuteAsync` faults `ExecuteTask`, and since .NET 6 the default
`BackgroundServiceExceptionBehavior.StopHost` then brings the whole application
down. One malformed digest would take the web server with it.

Swallowed is not the same as hidden. Every failure produces a log entry at
`Error` with the exception attached and a stable event id, plus a `Faulted`
ledger stamp carrying the reason, which the poll endpoint returns verbatim.

Observed, with three assignments submitted in order A, B, C where B asks for a
quote id that is absent from the catalogue:

```
Delivered  third@example.test     Assembled in 1524 ms.
Faulted    doomed@example.test    Quote 4242 is absent from the catalogue, so assignment 12cb9eaf-... cannot be assembled.
Delivered  first@example.test     Assembled in 3793 ms.
```

B faulted, C was still delivered, the pump never restarted, and
`/relay/vitals` reported `"handled":3`.

Worth noting *where* that failure happens. Quote id 4242 is a perfectly
well-formed integer, so request validation admits it; only a catalogue lookup
can reject it, and a catalogue lookup is exactly the work being kept off the
request thread. Some failures are only discoverable after the 202 has been sent,
which is why the ledger exists rather than being optional polish.

## 10. Testing performed

`dotnet test` — **31 passed, 0 failed**, about 3 seconds.

| Suite | Covers |
| --- | --- |
| `RelayIntakeTests` (9) | Acceptance below the ceiling; backlog accounting; refusal at the ceiling instead of growth; room reappearing after the consumer reads; a sealed intake refusing work; idempotent sealing; drain completing on a sealed and empty intake; drain parking on an empty one; a ceiling below one rejected at construction |
| `RelayPumpServiceTests` (9) | Queued work processed and stamped `Delivered`; work queued before start still processed; a throwing assignment leaving the pump alive; the assignment *after* a failure still running, in that order; a fresh DI scope per assignment; idle shutdown completing without faulting; in-flight work cancelled and stamped `Abandoned`; nothing left running after shutdown; a sealed and drained intake ending the loop on its own |
| `DigestAssemblyProcessorTests` (4) | Every requested quote reaching the finished digest; a missing catalogue id failing the assignment; a failed assignment leaving nothing on the shelf; cancellation stopping assembly partway |
| `DigestEndpointTests` (8) | `202` returned in under 750 ms for two seconds of work; the digest arriving later; a faulted assignment reported with its reason while the next one is delivered; five malformed-submission shapes rejected as `400`; an unknown assignment id returning `404` |
| `RelayShutdownTests` (1) | Real `IHost.StopAsync` sealing the intake, stamping in-flight work `Abandoned`, and finishing `ExecuteTask` as `RanToCompletion` |

Two things about how these are written. First, nothing under test is mocked: the
suites use the real channel, the real ledger and the real pump, substituting only
the processor — and `DigestEndpointTests` and `RelayShutdownTests` substitute
nothing at all, running the shipped application through
`WebApplicationFactory`. Second, every wait is signal-driven rather than timed:
`WatchfulLedger` hands out a task per (assignment, stage) pair so a test awaits
the exact transition it cares about and fails on a timeout instead of passing or
flaking on a guessed `Task.Delay`.

None of the tests reach into private state. They assert on the observable
surface — intake outcomes, ledger stages, shelf contents, HTTP responses, and
`ExecuteTask`'s status, which `BackgroundService` exposes publicly for exactly
this purpose. The one apparent exception, `ProcessorScript.Instantiations`,
counts constructor calls made by the container: it is how the suite observes
per-assignment scoping, which is a behavioural requirement rather than an
implementation detail.

Manual verification is recorded in `evidence/observed-run.md`, captured against
a running instance.

## 11. The one-line answer

Choose Hangfire over a hosted service when background work must be durable,
scheduled or recurring, retryable across restarts, and operationally observable.
