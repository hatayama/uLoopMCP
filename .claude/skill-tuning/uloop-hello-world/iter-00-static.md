# Iteration 0 — static audit (uloop-hello-world)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| HW1 | description lacked enumerated When and How suffix (was a one-line "Sample hello world tool…") | medium |
| HW2 | Body `## Output` already lists fields (`Message`, `Language`, `Timestamp`) — sufficient | none |
| HW3 | "Notes" section refers to "MCP tool" but project is CLI-first per CLAUDE.md (uloop CLI is primary, MCP is separate) — minor terminology mismatch | low |

## Iter 0 fix landed

HW1 only. Rewrote description into enumerated When (verify pipe healthy / reference minimal MCP tool implementation) + How suffix.

HW3 deferred — terminology drift not user-facing on this skill's main use cases (verify pipe + reference impl).
