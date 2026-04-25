# Iteration 0 — static audit (uloop-run-tests)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| RT1 | description lacked CLI invocation hint and the **single-flight** safety constraint that lives in project CLAUDE.md but not in the skill body | high (downstream-caller risk: dispatcher could try to parallelize `uloop run-tests` and freeze Unity) |
| RT2 | `--filter-value` is documented as "no default" but the table doesn't say it becomes required when `--filter-type` is not `all` | medium |
| RT3 | Unity-not-running case absent (would the call hang? error?) | medium |

## Iter 0 fix landed

RT1 only. Appended to description: "Executes via `uloop run-tests` CLI invocation (single-flight only — never run multiple `uloop run-tests` in parallel); auto-saves NUnit XML results to `.uloop/outputs/TestResults/` on failure."

RT1 is unusual — it patches the description with a safety constraint normally reserved for the body. Rationale: this is a high-stakes downstream-caller risk (Unity Editor freeze) that a closed-book reader will absolutely miss without prominent placement. The description is the densest signal an agent reads.

RT2 and RT3 deferred to Iter 1 verification.
