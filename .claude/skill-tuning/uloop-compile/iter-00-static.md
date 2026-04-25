# Iter 0 — Static description / body consistency check

Target: `.claude/skills/uloop-compile/SKILL.md` (61 lines, baseline frozen at commit-of-record before this iteration)

## Approach

Per the upstream `empirical-prompt-tuning` skill: **before any subagent dispatch, reconcile the description and the body so the executor cannot reinterpret the body to match the description (false-positive accuracy).**

The Iter-0 fixes are intentionally minimal: only resolve description ↔ body gaps and CLAUDE.md guideline violations. Do not add new operational guidance — that is reserved for Iter 1+ where empirical evidence drives the change.

## Observed gaps

### G1. description omits "How" component (CLAUDE.md guideline violation)

CLAUDE.md `Skill Description Guidelines` requires `What → When → How`. The current description has:
- What: "Compile Unity project and report errors/warnings"
- When: "Use when you need to: (1) Verify code compiles after C# file edits, (2) Check for compile errors before testing, (3) Force full recompilation with Domain Reload"
- How: **missing** ("Returns error and warning counts" describes Output, not implementation)

Fix: append "Executes via `uloop compile` CLI invocation."

### G2. description does not surface `--wait-for-domain-reload` flag

The body documents `--wait-for-domain-reload` as a parameter, and Examples line 39 combines it with `--force-recompile`. But the description's "When" item (3) only mentions "Force full recompilation with Domain Reload", which a reader would interpret as `--force-recompile` alone — the wait flag is invisible at the description level.

Fix: clarify When item (3) wording so it implies the wait option exists, or leave description as-is and let Iter 1 surface whether scenarios actually trigger it. **Decision: leave description as-is for Iter 0.** Adding the flag here pre-judges what Iter 1 will discover. If subagents stumble on the wait flag in Scenario B, we fix in Iter 1.

### G3. body has no recovery guidance for "Unity not running" state

CLAUDE.md global rule says "Unityが未起動なら `uloop launch -r` で起動", but `uloop-compile/SKILL.md` itself contains no such guidance. The body's Troubleshooting section only covers stale lock files (`uloop fix`), not the no-process case.

Fix: **leave body as-is for Iter 0.** This is exactly the kind of gap Iter 1 hold-out scenario H is designed to surface. Pre-fixing it would mask the empirical signal.

### G4. body uses `--force-recompile true --wait-for-domain-reload true` (positional bool) but Parameters table says `boolean` type with no value example

Lines 37-42 of SKILL.md show four combinations passing `true`/`false` as positional arguments, but the Parameters table only marks them as `boolean` defaulting to `false`. A subagent might infer that `--force-recompile` is a bare flag (no value) — the typical CLI convention.

Fix: **leave body as-is for Iter 0.** This is also a genuine empirical signal — if Scenario B subagents pick wrong invocation form, that tells us the body needs disambiguation.

## Iter-0 scope

Apply only G1 (CLAUDE.md guideline violation, no empirical judgement needed). G2/G3/G4 are flagged here for tracking but deferred to subsequent iterations where subagent reports will inform the fix wording.

## Diff to apply

```
- description: "Compile Unity project and report errors/warnings. Use when you need to: (1) Verify code compiles after C# file edits, (2) Check for compile errors before testing, (3) Force full recompilation with Domain Reload. Returns error and warning counts."
+ description: "Compile Unity project and report errors/warnings. Use when you need to: (1) Verify code compiles after C# file edits, (2) Check for compile errors before testing, (3) Force full recompilation with Domain Reload. Executes via `uloop compile` CLI invocation; returns error and warning counts."
```

## Tracking carry-over

Open observations to revisit in Iter 1+:
- G2: wait-flag visibility at description level
- G3: missing "Unity not running" recovery (expect hold-out H to surface this)
- G4: bool argument form ambiguity (expect Scenario B to surface this)
