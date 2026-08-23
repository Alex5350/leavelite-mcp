# LeaveLite

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
[![C#](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
[![MCP](https://img.shields.io/badge/MCP-Model%20Context%20Protocol-191919)](https://modelcontextprotocol.io)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A **leave / PTO management server for the Model Context Protocol**: an AI assistant that
speaks MCP - Claude Desktop, Claude Code, IDE agents - can check an employee's balance,
book leave, run the manager approval workflow and review team coverage through it.

Built with the **official MCP C# SDK** ([`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol/),
co-maintained by Microsoft and Anthropic) on ASP.NET Core, wrapped in Clean Architecture
with a pure, fully-tested accrual domain. Not a calculator demo: the domain carries the
rules a real HR system fights about - tenure gates, carry-over caps, holiday-aware
consumption, approval authority and minimum team staffing.

> This is a personal reference application - a deliberate exercise in exposing a serious
> DDD backend to AI clients through MCP. It pairs with
> [LedgerLite](https://github.com/Alex5350/ledgerlite) (REST API) and
> [LedgerLite Web](https://github.com/Alex5350/ledgerlite-web) (Blazor) as a set.

## What it exposes

| | Name | Purpose |
|---|---|---|
| Tool | `check_employee_balance` | Accrued / consumed / available hours per leave type |
| Tool | `forecast_balance` | Balance now vs. projected N months ahead |
| Tool | `request_leave` | Submit a leave request (overlap + balance guarded) |
| Tool | `cancel_leave_request` | Cancel pending, or approved-before-it-starts |
| Tool | `list_pending_approvals` | A manager's queue with hours and reasons |
| Tool | `decide_leave_request` | Approve/deny with authority + minimum-staffing enforcement |
| Tool | `get_team_calendar` | Who is out, holidays and working days per date |
| Tool | `list_holidays` | The organization's holiday calendar |
| Tool | `list_employees` / `who_reports_to_manager` | Directory data LLM callers need |
| Resource | `leavelite://policies`, `leavelite://teams`, `leavelite://holidays/{year}` | Reference data for client context |
| Prompt | `team-coverage-review` | Instructs an AI to audit coverage for a month using the tools |

Every failure surfaces as readable text carrying a **stable domain error code** -
`[LeaveRequest.OverlappingRequest]`, `[Employee.ApproverNotTeamManager]`,
`[LeaveRequest.MinimumStaffingNotMet]` - so an AI client can explain *why* an action failed.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Nothing else.

```bash
git clone https://github.com/Alex5350/leavelite.git
cd leavelite
dotnet run --project src/LeaveLite.Server
# info: Now listening on: http://localhost:5020   (MCP: /mcp, health: /health)
```

First run migrates SQLite and seeds a demo organization: five employees
(`ada@leavelite.io` is the manager), three accrual policies, the 2026 holiday calendar and
sample requests dated relative to *run time* so the demo never goes stale.

### Connect an MCP client

**Claude Desktop / Claude Code** - add to the MCP configuration:

```json
{
  "mcpServers": {
    "leavelite": {
      "type": "http",
      "url": "http://localhost:5020/mcp"
    }
  }
}
```

Then ask: *"Check Ada's vacation balance and forecast it three months out"* or
*"Show Bruno's pending request and approve it if the team calendar holds up."*

**MCP Inspector** (Anthropic's interactive tooling):

```bash
npx @modelcontextprotocol/inspector dotnet run --project src/LeaveLite.Server
```

**Raw protocol** - stateless Streamable HTTP:

```bash
curl -s http://localhost:5020/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

## Architecture

```
src/
├── LeaveLite.Domain/            # Pure model: aggregates, value objects,
│   ├── Balances/                #   AccrualBalanceCalculator (pure, deterministic)
│   ├── Specifications/          #   Eligibility + minimum-staffing, combinable
│   └── ...                      #   typed error catalog, domain events
├── LeaveLite.Application/       # Use cases: CQRS handlers, ports, validation
├── LeaveLite.Infrastructure/    # EF Core 10 + SQLite, migrations, event channel worker
└── LeaveLite.Server/            # MCP host: tools/resources/prompts over /mcp
tests/
├── LeaveLite.Domain.UnitTests/          # 166 tests - accrual math in depth
├── LeaveLite.Application.UnitTests/     # 60 tests - every use case, every branch
└── LeaveLite.Server.Tests/              # 27 tests - the official MCP client speaking
                                         #   the real protocol against the real host
```

Every architectural decision - and the challenges each one surfaced - is recorded in
[docs/adr/](docs/adr/): clean architecture, the SDK/transport choice, persistence, CQRS
without a mediator, accrual purity, error surfacing, domain events over a bounded channel,
and protocol-level testing. The build order lives in [docs/process.md](docs/process.md).

### The domain in one paragraph

Balances are computed, not stored: a pure accrual engine derives accrued hours from the
policy (tenure-gated monthly fractions or upfront grants, annual caps) and subtracts
approved leave measured in **working days** - weekends and organization holidays never
consume balance. `LeaveRequest` is a state machine (Pending → Approved/Denied/Cancelled)
whose transitions are illegal-transition-proof. Approval requires a same-team manager and
a `MinimumStaffingSpecification` check against the rest of the team's approved leave.
Every mutation runs through CQRS handlers returning typed `ErrorOr` results.

## Challenges worth reading

These came up during the build and are documented with their resolutions in the ADRs:

- **EF Core × rich constructors** - complex properties (`DateRange`) cannot be
  constructor-bound into aggregate constructors; aggregates carry private reconstitution
  constructors while factories remain the only business path.
- **Stateless HTTP vs. sessions** - stateless Streamable HTTP trades server affinity for
  trivial load balancing and simpler tests; the SDK's session semantics are available when
  needed.
- **Static tool classes** - the SDK's `WithTools<T>()` rejects static classes; assembly
  scanning is the supported discovery route.
- **Testing the transport, not the implementation** - the protocol suite connects the real
  SDK client through `WebApplicationFactory`'s in-memory server by injecting the factory's
  `HttpClient` into the transport (an API detail discovered against SDK 2.2.0).
- **Time correctness** - every clock read flows through `IDateTimeProvider`; the accrual
  engine takes `asOf` as a parameter, making the riskiest math pure and deterministic.
- **Error vocabulary for LLMs** - stable machine-readable codes paired with human-readable
  sentences, because an AI client must explain failures it didn't expect.

## Testing

```bash
dotnet test        # 253 tests, no setup required
```

The protocol suite is the differentiator: 27 tests perform the initialize handshake,
discover tools/resources/prompts, run the full request → approve → balance-reduced write
flow, and assert domain error codes through tool results - the exact experience a Claude
client gets.

## Tech stack

- .NET 10 / C# 14, ASP.NET Core
- [ModelContextProtocol C# SDK 2.2.0](https://github.com/modelcontextprotocol/csharp-sdk) (Microsoft + Anthropic)
- EF Core 10 + SQLite, ErrorOr, FluentValidation, NSubstitute, xUnit v3, Serilog

## License

[MIT](LICENSE)
