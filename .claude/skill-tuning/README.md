# skill-tuning artifacts

This directory stores per-skill artifacts produced by the `empirical-prompt-tuning` skill (https://github.com/mizchi/skills/tree/main/empirical-prompt-tuning).

## Layout

```
.claude/skill-tuning/
  README.md              <- this file
  <skill-name>/
    scenarios.md         <- 2-3 evaluation scenarios (1 median + 1-2 edge) + 1 hold-out
    checklist.md         <- requirements checklist per scenario, with at least one [critical]
    ledger.md            <- failure pattern ledger (per-target, NOT shared across skills)
    iter-00-static.md    <- Iter 0 static description/body consistency check
    iter-NN.md           <- one file per iteration (NN = 01, 02, ...)
```

## Rules

- **One ledger per target skill.** Failure patterns from one skill must not be merged into another's ledger — surface wording differs by domain.
- **Scenarios are fixed before Iter 1.** Once scenarios.md and checklist.md are committed, do not edit them in response to a fix. Editing scenarios to make unclear points disappear is a red flag explicitly called out by the upstream skill.
- **One theme per iteration.** Each `iter-NN.md` records changes for a single semantic theme. 2-3 related micro-fixes can be bundled, unrelated fixes go to the next iter.
- **Hold-out scenario is reserved for convergence judgment.** Do not use it during normal iterations.

## Convergence

Per the upstream skill, stop when 2 consecutive iters satisfy all of:
- New unclear points: 0
- Accuracy improvement vs previous: ≤ +3 points
- Step count variation vs previous: within ±10%
- Duration variation vs previous: within ±15%
- Hold-out scenario does not drop ≥ 15 points from the recent average

## Rollout priority

`priority-matrix.md` ranks remaining skills for tuning attention based on description compliance, hidden complexity, and downstream-caller risk. Pick the next batch from the top of that ranking; skills below total score 4 are deferred unless a real downstream issue forces a re-evaluation.

`priority-matrix-vs-measured.md` records what the matrix predicted vs what the rollout actually surfaced. The two biggest misses (description "How" accuracy, missing `## Output` sections) inform the static linter described below.

## Static triage linter

Before reaching for the matrix, run the static linter to catch the gaps that reliably caused fabrication during the rollout:

```bash
cd Packages/src/Cli~
npm run lint:skills
```

Scope: every bundled `SKILL.md` under `Packages/src/Editor/Api/McpTools/*/Skill/` and `Packages/src/Cli~/src/skills/skill-definitions/cli-only/*/Skill/` (skills with `internal: true` are skipped).

Hard errors (exit non-zero):
- `DESC-001` description missing from frontmatter
- `DESC-002` description has no When clause (`Use when ...` or `(1) ... (2) ...`)
- `DESC-003` description has no How tail (mechanism keyword like `via`, `Routes through`, `Executes`, `uloop CLI`, ...)
- `BODY-001` body has no `## Output` section (worst case from the rollout — forces invented fields)
- `BODY-002` `## Output` is a one-line stub

Warnings: short description, missing usage / parameters / examples sections (with established aliases like `Tool Reference`, `Workflow`, `Code Examples by Category` accepted), output section that does not look like a field enumeration.

Run this on every new bundled skill before the closed-book Iter 1 — it costs less than one subagent dispatch and would have caught the get-hierarchy / simulate-mouse-input/ui / simulate-keyboard misses found during the manual rollout.
