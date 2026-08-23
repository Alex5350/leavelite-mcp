# ADR 0005: The accrual engine is a pure function with an injected clock

**Status:** Accepted

## Context
Balance math is the riskiest logic in the product: tenure gates, monthly fractions,
annual caps, upfront grants, carry-over and holiday-aware consumption. Subtle time bugs
here are silent financial errors.

## Decision
`AccrualBalanceCalculator.Calculate(employee, policy, asOf, history, holidays)` is pure and
deterministic: `asOf` is a parameter, never read from the system clock. Every consumer
that needs "now" receives it from `IDateTimeProvider` (one implementation:
`SystemDateTimeProvider`), so no production or test code touches `DateTime.Now`.

## Consequences
- The deepest business logic is testable with plain data: no mocks, no time travel
  frameworks; the 166-test domain suite pins boundary cases (Feb-28 clamp, exact 2-decimal
  mid-period fractions).
- Balance reproducibility: the same inputs always yield the same balance, which is what an
  auditor or a reconciliation job needs.
- Negative balances are surfaced, not clamped: an overdraw is a signal someone must see.

## Challenges
- Carry-over caps interact with the calendar year boundary; the calculator documents the
  chosen semantics (cap applied at year roll) instead of hiding it behind configuration.
- Upfront-grant policies (Sick, Parental) and accrual policies (Vacation) force one engine
  to serve two grant models, handled by a policy flag rather than parallel code paths.
