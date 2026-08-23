# ADR 0008: Integration tests speak the protocol, not the implementation

**Status:** Accepted

## Context
The server's contract is MCP. Testing handler methods directly would prove the wrong thing.

## Decision
The server test suite boots the real host with `WebApplicationFactory<Program>` (unique
seeded SQLite database per class) and connects the **official MCP C# client** over
Streamable HTTP, the same path Claude Desktop takes: initialize handshake, capability
discovery, tool calls, resource reads, prompt retrieval, and a full write flow
(request → pending list → approval → reduced balance).

## Consequences
- Transport, serialization, DI wiring, seeding and domain rules are exercised together;
  a regression in any layer fails a protocol test.
- Tests assert on substrings and stable error codes, never exact prose.

## Challenges
- `WebApplicationFactory`'s in-memory server exposes `http://localhost` with no real port;
  the SDK transport must be constructed with the factory's own `HttpClient`
  (`HttpClientTransport(options, httpClient, ...)`), discovered via reflection against
  SDK 2.2.0.
- Seeded demo requests are relative to run time so the dataset never goes stale; write-flow
  tests anchor on weeks far beyond seeded data to avoid overlap collisions.
