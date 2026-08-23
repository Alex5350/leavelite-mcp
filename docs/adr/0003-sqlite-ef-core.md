# ADR 0003: SQLite behind EF Core with real migrations

**Status:** Accepted

## Context
Reviewers should be able to clone and run with zero infrastructure. The persistence model
must still exercise real relational mapping decisions.

## Decision
EF Core 10 on SQLite with a checked-in migration. Typed ids map through value converters;
`DateRange` maps as a complex property flattened to start/end columns; holiday calendars
persist as a JSON column behind a row type because the domain calendar is a computed,
read-only collection.

## Consequences
- `dotnet run` is the entire setup; the database migrates and seeds on first start.
- Switching providers is a connection-string change plus one migration.
- Enums persist as readable text, which keeps the demo database inspectable.

## Challenges
- Get-only properties are not convention-mapped in EF 10; every one needs an explicit
  `Property()` call in configuration.
- The connection string must resolve lazily (inside the options lambda) so host-level
  configuration overrides (tests, containers) reliably win.
