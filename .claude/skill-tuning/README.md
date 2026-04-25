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
