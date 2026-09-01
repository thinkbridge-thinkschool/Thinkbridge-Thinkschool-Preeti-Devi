# Day 19 — evidence (real Azure Service Bus)

Captured 2026-09-01 by `scripts/verify-azure.ps1` and `scripts/verify-shutdown.ps1`,
against a **real Azure Service Bus namespace**. No emulator, no simulation.

| | |
| --- | --- |
| Subscription | `Azure for Students` — `4d89877c-3cf4-491f-b999-03c9ff6bc7c3` |
| Signed in as | `shubh.rastogi2@s.amity.edu` |
| Resource group | `thinkschool-rg` |
| Namespace | `sb-day19-quotedemo` (**Standard**, `eastasia`) — pre-existing, reused |
| Endpoint | `https://sb-day19-quotedemo.servicebus.windows.net:443/` |
| Topic | `quote-events` |
| Subscriptions | `sub-a`, `sub-b` — both `MaxDeliveryCount = 3` |
| Auth | `DefaultAzureCredential` → Azure CLI identity. No connection string or key. |

**Nothing was provisioned.** The namespace, topic and both subscriptions already existed and
were empty (0 active, 0 dead-lettered) when this work started, so they were reused as-is. No
Azure resource was created, modified or deleted.

---

## 1. Publisher — MessageId is the event id

`POST /events` → `publisher-output.json`

```json
{
  "messageId":     "0612a40d-42b7-4d96-bc29-7ef1cce24c09",
  "correlationId": "day19-101-0612a40d42b74d96bc297ef1cce24c",
  "topic":         "quote-events",
  "fansOutTo":     [ "sub-a", "sub-b" ]
}
```

The `messageId` is the caller's `eventId`, unchanged — that is the idempotency key.

## 2. Both subscriptions received the same event

```
[sub-a] worker sub-a#1 handled MessageId=0612a40d-… quoteId=101 type=QuotePublished delivery=1.
[sub-b] worker sub-b#2 handled MessageId=0612a40d-… quoteId=101 type=QuotePublished delivery=1.
```

One publish, one topic, two subscriptions. On a queue only one consumer would have got it.

## 3. Duplicate MessageId suppressed

The same `eventId` published a second time. `duplicate-check.json`:

```
before duplicate:  { "sub-a": 1, "sub-b": 1 }
after  duplicate:  { "sub-a": 1, "sub-b": 1 }
```

```
[sub-a] worker sub-a#2 duplicate ignored: MessageId=0612a40d-… delivery=1. Completing without reprocessing.
[sub-b] worker sub-b#1 duplicate ignored: MessageId=0612a40d-… delivery=1. Completing without reprocessing.
```

The business counters are plain increments, so a duplicate reaching the handler would have
moved them. They did not move. The duplicate was **completed**, not abandoned — the work had
already happened, so the message is finished.

## 4. Competing consumers

Two workers per subscription; a burst of 12 events. `competing-consumers.json`:

```
sub-a#1  handled 7      sub-b#1  handled 6
sub-a#2  handled 6      sub-b#2  handled 7

totalHandled 26   distinctMessageIds 13   workersThatDidWork 4
```

26 handled entries over 13 distinct message ids — 13 per subscription, none handled twice.
Azure's peek-lock is what guarantees that; the workers do not coordinate.

## 5. Poison message → REAL Azure DLQ (permanent failure)

Poison `MessageId = e72cdbb4-ea28-410a-8499-bce3ef3fceec`, `eventType = "UnsupportedEvent"`.

```
[sub-a] worker sub-a#1 dead-lettering MessageId=e72cdbb4-… Reason=UnsupportedEventType
[sub-b] worker sub-b#2 dead-lettering MessageId=e72cdbb4-… Reason=UnsupportedEventType
```

Read back from `quote-events/<sub>/$DeadLetterQueue` via the SDK (`dlq-messages.json`):

```
subscription               : sub-a
messageId                  : e72cdbb4-ea28-410a-8499-bce3ef3fceec
deadLetterReason           : UnsupportedEventType
deadLetterErrorDescription : Event type 'UnsupportedEvent' is not handled by this consumer
                             build. Supported types: QuotePublished, QuoteRetired.
deliveryCount              : 0

subscription               : sub-b
messageId                  : e72cdbb4-ea28-410a-8499-bce3ef3fceec
deadLetterReason           : UnsupportedEventType
deadLetterErrorDescription : (same)
deliveryCount              : 0
```

Dead-lettered on the first delivery by both subscriptions, with the consumer's own reason.
No retries — redelivery could not change the answer.

## 6. Transient failure → retried → DLQ at MaxDeliveryCount

Probe `MessageId = 3daf479a-4c7c-425c-a316-1a63e76eea35`, `eventType = "TransientFailureProbe"`.
Azure redelivered it three times, and it landed on a *different worker* each time:

```
worker sub-a#2 abandoning MessageId=3daf479a-… after delivery 1
worker sub-a#1 abandoning MessageId=3daf479a-… after delivery 2
worker sub-a#2 abandoning MessageId=3daf479a-… after delivery 3
```

Then dead-lettered **by Azure itself**, not by application code:

```
subscription               : sub-a
messageId                  : 3daf479a-4c7c-425c-a316-1a63e76eea35
deadLetterReason           : MaxDeliveryCountExceeded
deadLetterErrorDescription : Message could not be consumed after 3 delivery attempts.
deliveryCount              : 3
```

Same on `sub-b`. There is no retry policy in the codebase — this is native Service Bus
behaviour driven by `MaxDeliveryCount = 3`. The delivery counts distinguish the two DLQ
routes cleanly: **0** for the permanent message, **3** for this one.

## 7. Azure's own view — independent of the application

The run begins by draining both dead-letter queues (`DELETE /dlq`, which receives and
completes anything left there), so these counts belong to this run alone.

`az servicebus topic subscription show`, before and after (`azure-topology.json`):

```
BEFORE (after draining)                    AFTER
subscription  active  deadLettered         subscription  active  deadLettered
sub-a         0       0                    sub-a         0       2
sub-b         0       0                    sub-b         0       2
```

Azure's control plane reports 2 dead-lettered messages per subscription. This is read
straight from Azure by the CLI and does not depend on anything the app says.

## 8. Graceful shutdown

A real Ctrl+C sent to the host's console (`graceful-shutdown.log`):

```
15:34:07.797 info: Microsoft.Hosting.Lifetime[0] Application is shutting down...
15:34:07.833 info: Worker sub-b#2 on sub-b stopped cleanly.
15:34:08.008 info: Worker sub-b#1 on sub-b stopped cleanly.
15:34:08.303 info: Worker sub-a#2 on sub-a stopped cleanly.
15:34:08.589 info: Worker sub-a#1 on sub-a stopped cleanly.
```

All four processors stopped and were disposed. Nothing timed out ("still had work in
flight" does not appear).

---

## Files

| File | Content |
| --- | --- |
| `azure-transcript.txt` | Full run transcript, topology and results |
| `azure-topology.json` | Namespace/topic/subscription identifiers, before/after counts |
| `publisher-output.json` | The `POST /events` receipt |
| `consumer-output.log` | Full consumer log |
| `duplicate-check.json` | Counters either side of the duplicate |
| `competing-consumers.json` | Messages handled per worker |
| `dlq-messages.json` | Both dead-lettered messages, read from the real DLQ |
| `message-ids.json` | Poison, probe and first-event message ids |
| `subscription-state.json` | Final per-subscription state and the ledger |
| `graceful-shutdown.log` | The Ctrl+C shutdown run |

## Note on leftover state

Four dead-lettered messages remain — two per subscription, from this run. They were left in
place deliberately so the evidence can be re-inspected in the portal or with
`az servicebus topic subscription show`. They cost nothing to keep, and the next
`verify-azure.ps1` run drains them first so its evidence stands alone. To clear them by hand:
`curl -X DELETE http://localhost:5219/dlq` with the app running.
