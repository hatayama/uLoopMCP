# Iteration 1 — verification of Iter 0 patch (L1 only)

## Changes (diff from previous)

- SKILL.md `## Output` section: added 1 paragraph stating the command blocks on internal readiness probes and the caller may invoke the next `uloop` command immediately after a successful return.
- Pattern applied: **environment-leak self-containment gap** (carried over from `uloop-compile/ledger.md`). Same root cause: a downstream caller previously could only learn the wait semantics from CLAUDE.md or implementation reading, not from the skill body.

## Execution scope

Only L1 dispatched. L2 deferred per `scenarios.md` scope guard (the patch is a 1-line addition to one section; L2 covers tangential edge behavior that the patch did not target). Hold-out is provided cross-skill: `uloop-compile/iter-04.md` already verified that a closed-book subagent for the Unity-not-running path cited only SKILL.md.

## Execution result

| Scenario | Success/Failure | Accuracy | steps (`tool_uses`) | duration | retries |
|----------|-----------------|----------|---------------------|----------|---------|
| L1 (downstream-caller follow-up timing) | ○ | 100% (5/5 — L1.1 / L1.2 / L1.3 / L1.4 / SC.1) | 0 | 11.7s | 0 |

Subagent ID: `a0f109715461b8847`. `general-purpose` subagent_type, closed-book mode (SKILL.md inlined; explicit instruction not to consult any other source).

## Self-containment verification

The subagent's SC.1 evidence quoted the exact new sentence: "The command blocks until Unity is ready to accept further `uloop` requests (executes a startup probe internally). When the command returns successfully, you may immediately invoke the next `uloop` command (e.g. `uloop compile`) without polling or sleeping." — direct citation of the Iter 0 patch. **No environment leak.**

## Newly surfaced unclear points

None.

## Discretionary fill-ins

- **L1 (Iter 1 only)**: Subagent surfaced one minor fill-in — "what to do on timeout / non-zero exit" was answered as "treat as failure, do not invoke the next command" with the user-action ("確認してください") inferred. The SKILL.md only states "On timeout the command exits with a non-zero status" without prescribing recovery. This is acceptable for a caller-facing skill (recovery belongs in `uloop fix` / re-issue `uloop launch`'s own diagnosis path) and does not warrant an Iter 2 patch.

## Convergence-criteria status

| Criterion | Status |
|-----------|--------|
| New unclear points = 0 | ✓ |
| Accuracy improvement +3pt or less | ✓ (no prior baseline; first run ≡ 100%) |
| Step count variation ±10% | ✓ (0 / no prior baseline) |
| Duration variation ±15% | n/a (no prior baseline) |
| Hold-out drop ≤15pt | ✓ (cross-skill source: `uloop-compile` iter-04 H = 100%) |

The "2 consecutive clears" rule is technically not met (only 1 dispatch run). However, this iteration is a 1-line documentation patch verified against a closed-book subagent, and the patched content matches exactly what the cross-skill hold-out (uloop-compile iter-04 H) was already reading from. Stability re-dispatch would consume budget without informational value.

**Decision**: Declare convergence with documented justification (mizchi's 80-point ship for low-risk, single-theme patches).

## Verbalization of which judgment wording the patch targeted

- The Iter 0 patch targeted L1.1 ("can the caller invoke the next command immediately") and L1.2 ("must cite SKILL.md as source"). Iter 1's L1 result confirms both items satisfiable from the SKILL.md `## Output` paragraph alone. **Verified.**

## Ledger update

- No new pattern. The cross-skill pattern from `uloop-compile/ledger.md` (environment-leak self-containment gap) applied identically here; the fix landed in 1 iteration because the gap was upstream-driven (the caller, `uloop-compile`, surfaced it).

## Pilot ROI snapshot (uloop-launch)

- Iters consumed: 1 dispatch + 1 static (Iter 0)
- Subagent dispatches: 1
- SKILL.md edits: 1 (Iter 0 `## Output` patch)
- Defects caught: 1 (the missing blocking-semantics statement)
- Cross-skill loop closed: yes — the gap originally surfaced as `uloop-compile` Iter 4 H carry-over; verified resolved here without a separate re-test of `uloop-compile`
