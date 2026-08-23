# ADR 0002: Official MCP C# SDK over stateless Streamable HTTP

**Status:** Accepted

## Context
The server must be consumable by MCP clients such as Claude Desktop, Claude Code and IDE
agents. Two implementation options existed: hand-rolled JSON-RPC, or the official SDK.

## Decision
Use `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` 2.2.0 (the C# SDK
co-maintained by Microsoft and Anthropic) hosted by ASP.NET Core via
`AddMcpServer().WithHttpTransport(o => o.Stateless = true)` and `MapMcp("/mcp")`.
Tools, resources and prompts are discovered with `WithToolsFromAssembly` /
`WithResourcesFromAssembly` / `WithPromptsFromAssembly`.

## Consequences
- Full protocol compliance (initialize handshake, capability negotiation, SSE responses)
  without writing transport code; verified by the protocol test suite.
- Stateless mode keeps every POST self-contained, which makes load balancing trivial and
  lets integration tests skip session bookkeeping.
- All three MCP primitives are exposed (tools, resources, prompts), so the server
  demonstrates the complete surface, not a subset.

## Challenges
- `WithTools<T>()` in current SDK versions rejects static tool classes (CS0718); assembly
  scanning is the supported route.
- The negotiated protocol revision drops `ping`; tests assert capabilities that exist.
- Authentication is intentionally out of scope for the portfolio build; the endpoint is
  anonymous in Development and the production posture (OAuth resource server / bearer) is
  left as a documented extension point.
