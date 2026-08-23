# ADR 0007: Domain events over a bounded channel instead of a broker

**Status:** Accepted

## Context
`LowBalanceWarningDomainEvent` (raised when a request leaves less than one workday of
balance) must reach "whoever alerts people" without blocking the request flow or forcing
every clone to run Kafka.

## Decision
The dispatcher logs every event and writes low-balance warnings into a bounded
`Channel<T>` (capacity 64, drop-write under pressure) drained by a `LowBalanceAlertWorker`
background service that emits a structured warning.

## Consequences
- Backpressure behavior is explicit: under sustained load we drop an alert rather than
  block a leave request: the right trade-off for a warning, and the exact seam where a
  real deployment would plug in email/Slack/PagerDuty via an outbox.
- The pattern (events → channel → worker) is recognizable from Microsoft's hosting docs,
  demonstrating in-process async pipelines with zero infrastructure.

## Challenges
- Ordering is not guaranteed under drop-write; consumers must be idempotent by design.
- A dropped event is silently lost unless the worker logs the drop; it does.
