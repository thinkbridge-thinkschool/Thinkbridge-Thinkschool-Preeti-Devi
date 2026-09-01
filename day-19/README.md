# Day 19 — Azure Service Bus topic, two subscriptions, DLQ

A small ASP.NET Core app that publishes events to a real Azure Service Bus **topic** and
consumes them from **two subscriptions**, with competing consumers, MessageId-based
idempotency, and poison messages that land in the real dead-letter queue.

```
POST /events
     │
     ▼
  topic: quote-events
     ├──────────────┐
     ▼              ▼
  sub-a          sub-b            two subscriptions, each gets a copy
   │  │           │  │
  w1  w2         w1  w2           two competing workers per subscription
```

## Verified against real Azure

Run 2026-09-01 against `sb-day19-quotedemo`. Full evidence in
[`evidence/`](evidence/README.md).

| Requirement | Result |
| --- | --- |
| Publish to a real topic | `quote-events` on `sb-day19-quotedemo.servicebus.windows.net` |
| Exactly two subscriptions | `sub-a`, `sub-b` |
| Both subscriptions receive the event | `{ "sub-a": 1, "sub-b": 1 }` from one POST |
| Competing consumers | `sub-a#1=6 sub-a#2=7 sub-b#1=6 sub-b#2=7`; 13 distinct ids per subscription, none twice |
| MessageId idempotency | `messageId` = caller's `eventId`; duplicate left counters unchanged |
| Poison → real DLQ | `UnsupportedEventType`, delivery count 0, both subscriptions |
| MaxDeliveryCount → DLQ | Retried 3×, then `MaxDeliveryCountExceeded` by Azure itself |
| Azure's own view | `deadLetterMessageCount` 0 → 2 per subscription, read by `az` CLI |
| Graceful shutdown | Real Ctrl+C; all four workers "stopped cleanly" |
| Tests | 8/8 passing |

## Azure resources

| | |
| --- | --- |
| Subscription | `Azure for Students` — `4d89877c-3cf4-491f-b999-03c9ff6bc7c3` |
| Resource group | `thinkschool-rg` (existing) |
| Namespace | `sb-day19-quotedemo` — Standard, `eastasia` (existing, **reused**) |
| Topic | `quote-events` (existing, reused) |
| Subscriptions | `sub-a`, `sub-b`, `MaxDeliveryCount = 3` (existing, reused) |

**Nothing was created.** The namespace, topic and both subscriptions already existed and were
empty, so they were reused. `Microsoft.ServiceBus` was already registered. No Log Analytics,
Application Insights, Storage, Key Vault, Event Hubs or managed identities were created.
Standard tier is required because Basic does not support topics.

`scripts/provision-servicebus.ps1` creates this topology from scratch if it is ever needed in
another subscription.

## Publisher

`POST /events`

```json
{ "eventId": "d5308985-…", "quoteId": 101, "eventType": "QuotePublished" }
```

`EventPublisher` validates the request, then builds the message:

```csharp
MessageId     = quoteEvent.EventId.ToString("D")   // ← the idempotency key
CorrelationId = "day19-{quoteId}-{eventId}"
Subject       = eventType
```

`eventId` is optional. Supplying it is what makes a retried POST safe end to end: the same id
produces the same MessageId, and the consumers recognise the second copy as a duplicate. One
`SendMessageAsync` reaches both subscriptions — the fan-out is the broker's job.

## Two subscriptions and competing consumers

`SubscriptionWorker` is a `BackgroundService` wrapping one `ServiceBusProcessor`. `Program.cs`
registers `WorkersPerSubscription` (2) of them per subscription, so four workers run:
`sub-a#1`, `sub-a#2`, `sub-b#1`, `sub-b#2`.

The two workers on one subscription are plain peers with no coordination. Service Bus hands
each message to exactly one of them under a peek-lock — that is the whole of the
competing-consumer arrangement. The two *subscriptions*, by contrast, each receive their own
copy of every event. That is the difference between a topic and a queue: on a queue, whichever
consumer picked a message up would deny it to the other.

`AutoCompleteMessages = false`. Every message is settled by hand, because auto-complete would
complete it the moment the handler returned and throw away the abandon and dead-letter
decisions.

## Idempotency

`ProcessedEventLedger` keys on `(subscription, MessageId)`.

```
first delivery  → not in ledger → handle → record → CompleteMessageAsync
duplicate       → in ledger     → skip business logic → CompleteMessageAsync
```

Two details matter more than the data structure:

- **The key includes the subscription.** Both subscriptions are supposed to act on the same
  event, so a ledger keyed on MessageId alone would let whichever subscription got there
  second mistake a real delivery for a duplicate.
- **The entry is written only after the work succeeds.** Recording first would mean a message
  whose handler then failed could never be retried — it would look like a duplicate to its own
  redelivery.

A duplicate is **completed, not abandoned**: the effect it asked for has already happened, so
the message is finished.

**In production this must not be in memory.** Several instances would each keep their own copy
and none would see the others' work, so the guarantee is lost the moment you scale out or
restart. The real answer is a durable shared store — a table with `(subscription, messageId)`
as its primary key, written as a conditional insert, so the uniqueness constraint rather than
application code enforces once-only processing.

## Retry vs permanent failure

`EventDispatcher` returns one of three settlements, and `SubscriptionWorker` applies it.

| Situation | Settlement | What happens |
| --- | --- | --- |
| Handled, or a known duplicate | `Complete` | `CompleteMessageAsync` |
| Unsupported `eventType`, unparseable body | `DeadLetter` | `DeadLetterMessageAsync(reason, description)` on the first delivery |
| Any other exception | `Abandon` | `AbandonMessageAsync`; Azure redelivers, and `MaxDeliveryCount = 3` dead-letters it in the end |
| Cancelled mid-message | `Abandon` | Shutdown is not a failure — the message goes back, nothing is recorded |

There is no retry policy in this codebase. Transient retries are native Service Bus
redelivery, and the subscription's `MaxDeliveryCount` is what eventually gives up.

Two scripted event types exercise both paths:

- `UnsupportedEvent` — the poison message. Dead-lettered immediately with
  `DeadLetterReason = UnsupportedEventType`, delivery count 0.
- `TransientFailureProbe` — throws an ordinary exception every time. Abandoned, redelivered,
  and dead-lettered by Azure at `MaxDeliveryCountExceeded` after three deliveries.

## Graceful shutdown

`StopAsync` calls `ServiceBusProcessor.StopProcessingAsync`, which stops the receive loop and
then waits for handlers already running to return, so in-flight work finishes and settles. It
uses a fresh token with a bounded grace, not the caller's — the caller's is already cancelled,
and passing it would turn "wait for in-flight work" into "drop it immediately". The processor
is disposed in a `finally`; the shared `ServiceBusClient` is a DI singleton and is disposed by
the container.

## Authentication

`DefaultAzureCredential` — **no connection string, key, or secret anywhere**. Configuration
holds only the namespace hostname. Locally it uses your `az login` identity; on an Azure host
it would use that resource's managed identity with no code change.

One wrinkle worth knowing: this machine has the **Azure Connected Machine (Arc) agent**
installed, which advertises a managed-identity endpoint. `DefaultAzureCredential` tries it
first, and the attempt fails with a hard error rather than "credential unavailable", which
aborts the chain before it reaches the CLI identity. `ServiceBus:ExcludeManagedIdentity=true`
drops that link for local runs; leave it false when hosted in Azure.

The signed-in identity needs a Service Bus data role on the namespace — **Azure Service Bus
Data Owner** covers send, receive and dead-letter access. It was already in place here.

## Running it

```powershell
cd day-19/scripts
./verify-azure.ps1        # publish, fan-out, duplicate, poison, DLQ, evidence
./verify-shutdown.ps1     # real Ctrl+C, graceful shutdown evidence
```

Or run the app directly:

```powershell
dotnet run --project src/Day19.Events `
  --ServiceBus:FullyQualifiedNamespace sb-day19-quotedemo.servicebus.windows.net `
  --ServiceBus:ExcludeManagedIdentity true
```

| Endpoint | Purpose |
| --- | --- |
| `POST /events` | Publish one event to the topic |
| `GET /state` | What each subscription handled, and the ledger |
| `GET /dlq` | Peeks both dead-letter queues — MessageId, reason, description, delivery count |

## Tests

`dotnet test` — 8 tests, all offline, covering the five behaviours the exercise turns on:

1. the event id becomes the Service Bus MessageId;
2. a repeated MessageId is completed without being processed again (and the two subscriptions
   do not de-duplicate each other);
3. an unsupported event type is classified as permanent and dead-lettered (as is a malformed
   body);
4. a transient failure is abandoned for redelivery, and a failed message is not blocked from
   succeeding later;
5. cancelling mid-message abandons it rather than losing it.

The decision logic lives in `EventDispatcher`, separate from the Service Bus plumbing, which is
why these run without a namespace. Real-Azure verification is a separate step, by design.

## Exercise answer

> Use Hangfire when background work needs durable storage, scheduling/recurring execution,
> retries across restarts, and operational monitoring; use a hosted worker when simple
> in-process processing is sufficient.

## Files

```
day-19/
  src/Day19.Events/
    Program.cs               wiring, POST /events, GET /state, GET /dlq
    QuoteEvent.cs            event contract, event types, codec, PermanentEventException
    EventPublisher.cs        validation + ServiceBusMessage construction + send
    EventDispatcher.cs       what to do with a delivered message (all the logic)
    SubscriptionWorker.cs    BackgroundService + ServiceBusProcessor, settlement only
    ProcessedEventLedger.cs  idempotency store
    ServiceBusSettings.cs    configuration
  tests/Day19.Events.Tests/  8 offline tests
  scripts/                   verify-azure.ps1, verify-shutdown.ps1, provision-servicebus.ps1
  evidence/                  real Azure run — see evidence/README.md
```
