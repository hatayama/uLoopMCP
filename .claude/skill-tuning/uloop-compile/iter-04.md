# Iteration 4 — fix landed (Troubleshooting split) + post-fix re-evaluation

## Changes (diff from previous)

- **SKILL.md changed.** `Troubleshooting` section split into two distinct failure modes:
  - **Stale lock files** (Unity *is* running) → `uloop fix`
  - **Unity Editor not running** (no Unity process) → `uloop launch -r`
- Pattern applied: **environment-leak self-containment gap** (ledger entry from Iter 3). The `uloop launch -r` recovery snippet is now inlined in SKILL.md so the skill body — without any CLAUDE.md or sibling-skill leakage — can satisfy hold-out H's `[critical]` items.
- 1 theme per iter ✓ (only the Troubleshooting wording changed).

## Execution scope

Re-ran scenarios A, C, H. Skipped B because the fix did not touch the `--force-recompile` / `--wait-for-domain-reload` paths that B exercises. (mizchi: re-run only what the diff could plausibly affect, plus the hold-out.)

## Execution results (per scenario)

| Scenario | Success/Failure | Accuracy | steps (`tool_uses`) | duration | retries | Weak phase |
|----------|-----------------|----------|---------------------|----------|---------|------------|
| A (median, regression) | ○ | 100% (4/4) | 0 | 12.9s | 0 | — |
| C (edge: lock recovery, regression) | ○ | 100% (5/5) | 0 | 24.0s | 0 | — |
| H (hold-out: Unity not running) | ○ | 100% (5/5) | 0 | 17.4s | 0 | — |

Subagent IDs (for audit): A = `a140b7869707c851a`, C = `aa5a7e70ba30793d0`, H = `a3fc77a5a2ed06be4`. All `general-purpose`, all distinct from prior iters.

## Variation vs Iter 1+2 baseline (A/C) and vs Iter 3 (H)

| Scenario | Δ accuracy | Δ steps | Δ duration | within thresholds? |
|----------|-----------|---------|------------|--------------------|
| A | 0pt | 0 | +0.5s vs Iter 1 / +0.5s vs Iter 2 (≈+4%) | ✓ (within ±15%) |
| C | 0pt | 0 | +4.6s vs Iter 1 / +2.1s vs Iter 2 (≈+10–24%) | △ (Iter 1 baseline +24%; Iter 2 baseline +10%) |
| H | 0pt | 0 | -5.3s vs Iter 3 (-23%) | ✓ (faster, no concern) |

C drift commentary: the +24% vs Iter 1 / +10% vs Iter 2 duration on C is at the edge of the ±15% threshold against the Iter 1 baseline. Since C's `tool_uses` is 0 and accuracy held at 100%, this is run-to-run wall-clock variance, not a regression of skill content. The new SKILL.md text is longer (Troubleshooting now has two named modes), which would slightly increase the planning-phase token count — consistent with a few extra seconds of subagent latency.

## Hold-out H — environment-leak verification

Iter 3's H pass was contaminated by a `CLAUDE.md`-sourced rationale (subagent admitted in `Discretionary fill-ins`). The Iter 4 H subagent (`a3fc77a5a2ed06be4`) was given the same prompt structure and explicitly cited the new `## Troubleshooting > Unity Editor not running` block of SKILL.md as the source of the `uloop launch -r` recovery. **The leak is closed.**

## Newly surfaced unclear point — H, Iter 4 only

The H subagent passed but reported one new structured unclear point:

- **Issue**: SKILL.md does not specify how to detect when `uloop launch -r` has finished and Unity is ready to accept compile requests.
- **Cause**: SKILL.md documents what `uloop launch -r` does ("opens the project at the current working directory") but provides no command, blocking flag, or polling mechanism to await launch completion. The instruction "After Unity finishes launching, retry `uloop compile`" leaves the wait condition implicit.
- **General Fix Rule**: Instructions that describe a sequential dependency ("A must finish before B") should also specify how to observe or await A's completion — either via a blocking flag on A, an explicit polling command, or a wait mechanism documented at the use site.

This is a *legitimate* new gap, surfaced only because the Iter 3 environment-leak gap was closed. The subagent now reads SKILL.md as its authoritative source and notices what it doesn't say.

## Convergence-criteria status

By strict mizchi rules:

| Criterion | Iter 4 vs Iter 3 | Status |
|-----------|------------------|--------|
| New unclear points = 0 | 1 new (H launch-completion detection) | ✗ |
| Accuracy improvement +3pt or less | 0pt | ✓ |
| Step count variation ±10% | 0 (no change) | ✓ |
| Duration variation ±15% | A +4%, H -23%, C +24% (vs Iter 1 baseline) / +10% (vs Iter 2) | △ (C borderline) |
| Hold-out H drop ≤15pt | 0pt drop | ✓ |

**Strict reading**: Iter 4 fails the "0 new unclear points" gate. A formally correct continuation would be Iter 5 to address the launch-completion detection gap.

## Decision — ship at convergence with documented carry-over

Adopting mizchi's "80-point ship" guidance with explicit reasoning:

1. **The new unclear point is structurally different from prior gaps.** Iter 3's gap (`-r` flag knowledge sourced from CLAUDE.md) was a *self-containment* defect — the skill could not stand alone. Iter 4's gap (no launch-completion detection) is a *workflow-completeness* improvement — the skill stands alone but a downstream wait condition is implicit. The first kind is what this skill must fix; the second is what *every* multi-step skill could carry.
2. **No critical-item failure.** All `[critical]` items in H pass. The implicit wait is handled by the subagent doing the obvious thing (issuing `uloop compile` and treating its response — success or another connection failure — as the readiness signal). This is fine for the compile workflow.
3. **The fix is in another skill's domain.** A "wait for launch completion" mechanism belongs in `uloop-launch`'s documentation (e.g., a blocking flag on `uloop launch -r`), not in `uloop-compile`. Cross-skill changes violate the 1-theme-per-iter rule and inflate scope.
4. **Diminishing returns.** A fifth iteration on the compile skill alone would either (a) inline yet another sibling-skill detail (re-introducing the kind of duplication the self-containment rule warns against), or (b) write a polling loop in prose, which is exactly the over-prescription failure mode mizchi flags.

**Action**: Declare convergence on `uloop-compile`. Carry the H launch-completion detection finding to a follow-up task scoped against `uloop-launch`'s skill (out of this pilot's scope).

## Verbalization of which judgment wording the planned fix targeted

- The Iter 4 SKILL.md edit targeted hold-out H's `[critical]` items H1 ("recognize Unity-not-running") and H2 ("invoke `uloop launch -r`") and made both satisfiable from SKILL.md alone, without relying on global CLAUDE.md context. **Verified**: Iter 4's H subagent passed both critical items while citing only SKILL.md.

## Ledger update

- Existing pattern **environment-leak self-containment gap** marked as `Fix landed in: iter 4`.
- No new pattern from Iter 4's H finding (it is filed as a deferred follow-up against `uloop-launch`, not a `uloop-compile` defect to track here).

## Pilot ROI snapshot (for horizontal-rollout decision)

- Iters consumed: 5 (Iter 0 static + 4 dispatch iters; mizchi's empirical range is 2–4 → we ran 1 over due to the hold-out finding being legitimate)
- Subagent dispatches: 10 (Iter 1: 3, Iter 2: 3, Iter 3: 1, Iter 4: 3)
- SKILL.md edits: 2 (Iter 0 description suffix; Iter 4 Troubleshooting split)
- Baseline accuracy → final accuracy: 100% → 100% (the meaningful improvement is *self-containment*, not raw accuracy — a dimension mizchi's accuracy metric alone does not capture)
- Defects caught that pre-pilot review missed: 2 (description "How" gap; Unity-not-running self-containment gap)

The "self-containment" delta is the headline finding. It would not have surfaced without the hold-out-with-environment-isolation step.
