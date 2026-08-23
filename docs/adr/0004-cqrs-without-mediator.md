# ADR 0004: CQRS handlers without a mediator package

**Status:** Accepted

## Context
Use cases need a uniform shape (validate → load → execute → persist → dispatch) without
coupling callers to handlers.

## Decision
Commands and queries are records implementing `ICommand`/`IQuery<T>`; handlers implement
`ICommandHandler<,>` / `IQueryHandler<,>` and are registered by `AddApplication()` assembly
scanning. Tool classes in the server resolve the concrete handler types from DI.

## Consequences
- No reflection pipeline magic; startup is honest and debuggable.
- One less dependency; the pattern remains recognizable to any .NET developer who has used
  MediatR.
- Cross-cutting behaviors (validation) compose explicitly inside handlers.

## Challenges
- Handlers are `internal sealed`; tests resolve them through the same DI registrations the
  server uses, which keeps the test composition root identical to production's.
