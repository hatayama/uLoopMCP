# Failure-pattern ledger — uloop-run-tests

Per-target ledger.

| Pattern | First seen | Iter | Notes |
|---------|-----------|------|-------|
| Safety constraint lived in CLAUDE.md but not in skill body | RT1 | Iter 0 | High-stakes downstream-caller risk (Unity Editor freeze if `run-tests` is parallelized). Closed at Iter 0 by promoting "single-flight only" into the description (the densest signal an agent reads). |
| Response shape missing field present in implementation | Iter 1 #1 | Iter 1 | `CompletedAt` returned by `RunTestsResponse.cs` but absent from skill. Closed at Iter 2. |
| `XmlPath` semantics on success unclear | Iter 1 #1 | Iter 1 | Closed at Iter 2 with explicit "Empty string when no XML was saved" wording. |

## Cross-skill carryover (informational only)

The "safety constraint lives in CLAUDE.md but not in skill body" pattern is unique to this skill so far, but the principle generalizes: if a downstream caller could destroy state by reading only the skill (closed-book), promote the constraint into the description. Other Unity-freeze-risk surfaces (e.g., `uloop-execute-dynamic-code` with infinite loops) should be audited the same way in future iterations.
