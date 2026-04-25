# Iteration 3 — hold-out H evaluation + decision

## Changes (diff from previous)
- No SKILL.md change. This iter evaluates hold-out scenario H to decide whether convergence is genuine or overfit.
- Pattern applied: (n/a)

## Execution result — hold-out H

| Scenario | Success/Failure | Accuracy | steps (`tool_uses`) | duration | retries |
|----------|-----------------|----------|---------------------|----------|---------|
| H (hold-out: Unity not running) | ○ | 100% (5/5) | 0 | 22.7s | 0 |

Subagent ID: `ab6e6c7c3368e0468`

## Comparison vs recent average (A/B/C from Iter 1+2)

- Recent average accuracy: 100%
- H accuracy: 100% → drop of 0pt (within ±15pt threshold) → **no overfitting by the strict rule**

## But: hold-out H surfaced a new unclear point

The subagent passed all critical items, but the `Unclear points (structured)` section was non-empty:

- **Issue**: `-r` flag of `uloop launch -r` is not documented inside the `uloop-compile` SKILL.md.
- **Cause**: `uloop-compile` SKILL.md scope is limited to the compile command and contains no launch-command reference.
- **General Fix Rule**: A skill that handles a workflow stage adjacent to another `uloop` subcommand (e.g., requires the Editor to be running) should either inline the minimum recovery snippet or include an explicit pointer to the relevant sibling skill.

Critically, the subagent's `Discretionary fill-ins` openly declared:

> `uloop launch -r` の知識は uloop-compile スキルではなく CLAUDE.md のビルドルール（「Unityが未起動なら `uloop launch -r` で起動」）から補完した。

This is the smoking gun. The subagent passed because the project's `CLAUDE.md` is in its environment context. A subagent without that context (different project, or strict skill-isolation test) would have either:
- Defaulted to `uloop fix` (per the SKILL.md Troubleshooting), failing critical item H1, or
- Reported "no recovery path documented" and failed.

The H 100% score is therefore **environment-leak-dependent**, not skill-dependent. By mizchi's framework this is a real unclear point that warrants a fix.

## Decision

**Fix in Iter 4.** Apply the General Fix Rule above with one minimum theme:
- Extend the `Troubleshooting` section of `uloop-compile/SKILL.md` to acknowledge "Unity Editor not running" as a distinct failure mode (separate from stale lock files).
- Provide the minimum recovery snippet (`uloop launch -r`) inline at the SKILL.md level so the skill is self-contained, instead of relying on CLAUDE.md leaking into context.
- This is one semantic theme per mizchi's "1 theme per iter" rule.

## Verbalization of which judgment wording the planned fix targets

- Targets H1 (`[critical]` recognize Unity-not-running) and H2 (`[critical]` invoke `uloop launch -r`).
- Specifically, makes both items satisfiable from SKILL.md alone, without relying on global CLAUDE.md context.

## Ledger update planned for Iter 4

- New pattern: **environment-leak self-containment gap**
  - Example issue: `-r` flag knowledge sourced from CLAUDE.md, not SKILL.md
  - General Fix Rule: A skill must satisfy its own `[critical]` checklist using only its body. Treat any "I knew this from CLAUDE.md / sibling skill" admission in a subagent's `Discretionary fill-ins` as evidence of a self-containment gap, even when the scenario passes accuracy.
  - Seen in: iter 3
