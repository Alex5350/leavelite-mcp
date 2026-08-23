# ADR 0001: Clean Architecture with a pure domain layer

**Status:** Accepted

## Context
LeaveLite is a leave/PTO management server whose consumers are AI assistants speaking the
Model Context Protocol. The business rules (accrual, tenure gates, approval authority,
staffing constraints) are the product; the protocol is just a transport.

## Decision
Four projects with dependencies pointing inward:

- `LeaveLite.Domain`: aggregates, value objects, the accrual engine, specifications.
- `LeaveLite.Application`: use cases (CQRS handlers), ports (repositories, clock, dispatcher).
- `LeaveLite.Infrastructure`: EF Core + SQLite, repositories, background alert worker.
- `LeaveLite.Server`: the MCP host; tools are thin adapters over application handlers.

## Consequences
- The domain builds with a single package (ErrorOr) and no knowledge of MCP, EF or HTTP.
- Swapping SQLite for PostgreSQL, or MCP for a REST surface, touches only outer layers.
- Protocol tools stay thin: every rule lives where it is testable without a server.

## Challenges
- EF Core cannot constructor-bind complex properties (like `DateRange`) into a rich
  constructor; aggregates carry a private reconstitution constructor (`LeaveRequest`),
  with the factory remaining the only business path.
