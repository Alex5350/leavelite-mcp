# ADR 0006: Domain error codes surface verbatim through tool results

**Status:** Accepted

## Context
MCP tool results are strings consumed by an AI client. Domain failures are `ErrorOr` values
with stable codes (`LeaveRequest.OverlappingRequest`, `Employee.ApproverNotTeamManager`).

## Decision
Tools map every `ErrorOr` failure to a readable sentence prefixed with the stable code in
brackets. Success results are concise, human-readable summaries with the key data.

## Consequences
- An AI client can reason about *why* something failed and relay it faithfully; tests (and
  clients) can match on codes instead of prose.
- Authorization failures (approver not a team manager) read distinctly from conflicts
  (minimum staffing) and validation failures, mirroring HTTP semantics without HTTP.

## Challenges
- FluentValidation failures surface under property-name codes (`[End]`), while domain
  invariants use dotted codes, a minor inconsistency the tests document; unifying it is a
  small, contained refactor if the surface grows.
- Tool descriptions double as the product's UX for LLM callers: each one teaches when the
  tool should be used, because a mis-invoked tool is a support ticket.
