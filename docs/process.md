# Development process

The repository was built in the order a real service would come together: domain first,
use cases second, persistence third, the protocol host last, tests at each seam, and
decision records written as the decisions were made. Each entry matches a commit
(`git log --oneline`); ADRs live in [adr/](adr/).

## Phase 1: Repository

- `chore: initialize repository with build configuration`: central build properties,
  NuGet pinned to nuget.org, editorconfig, SDK global.json.

## Phase 2: Domain and use cases

- `feat(domain): add value objects, enums and entity base`: DateRange overlap semantics,
  normalized Email, typed record-struct ids, error catalog.
- `feat(domain): add Employee, AccrualPolicy, LeaveRequest aggregates and holiday calendars`:
  the request state machine with illegal-transition errors and domain events.
- `feat(domain): add pure accrual engine and specifications`: deterministic balance math
  and combinable eligibility/staffing specifications.
- `feat(application): add repository ports, unit of work, time provider and messaging
  abstractions`: the clock seam (`IDateTimeProvider`) is established here.
- `feat(application): add leave use cases and wire the application layer`: nine CQRS
  use cases with validation and ErrorOr results.

## Phase 3: Persistence and events

- `feat(infrastructure): add EF Core persistence layer`: mapping decisions incl. the
  private reconstitution constructor (ADR 0001 challenge).
- `feat(infrastructure): add repositories, unit of work and time provider`.
- `feat(infrastructure): add initial SQLite migration`.
- `feat(infrastructure): add channel-based domain event dispatcher and low-balance alert
  worker`: bounded channel, drop-write semantics (ADR 0007).
- `feat(infrastructure): wire services and seed a demo organization`: run-time-relative
  seed data so the demo never goes stale.

## Phase 4: The MCP surface

- `feat(server): host MCP server over Streamable HTTP`: SDK choice and stateless
  transport (ADR 0002).
- `feat(server): expose leave operations as MCP tools`: ten tools; error codes surface
  verbatim (ADR 0006).
- `feat(server): add MCP resources and a team-coverage prompt`: the complete MCP surface.

## Phase 5: Tests

- `test: add domain and application unit test suites`: 226 tests across the accrual
  engine, state machine, specifications and every use-case branch.
- `test: add MCP protocol integration suite`: 27 tests driving the real endpoint with
  the official SDK client (ADR 0008).

## Phase 6: Documentation

- `docs: add architecture decision records`: eight ADRs with consequences and challenges.
- `docs: write README and development process`: connection instructions for Claude
  Desktop, the MCP Inspector and raw curl; this file.

## Conventions

- Conventional Commits with why-bodies; every commit compiles.
- Time flows only through `IDateTimeProvider`; the accrual engine is pure.
- Bugs found by testing land as separate fix commits with the discovery story.
