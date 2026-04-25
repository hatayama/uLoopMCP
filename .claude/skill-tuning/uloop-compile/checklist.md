# Requirements checklist — uloop-compile

Frozen at Iter 1 baseline. Do not modify in response to fixes (red flag in the upstream skill).

Judgment rules: ○ = full satisfaction, × = clearly missed, partial = 0.5. Success/failure is **binary** — `[critical]` items must all be ○ for success (○). Any `[critical]` × or partial → failure (×). Accuracy = (sum of points) / (item count) × 100.

## Scenario A — median (4 items)

1. **[critical]** The plan invokes `uloop compile` (no flags required) as the primary action.
2. **[critical]** The plan reads `ErrorCount` and `WarningCount` from the JSON output and reports both back to the user.
3. The plan does not invoke unrelated `uloop` subcommands (e.g., `uloop fix`, `uloop launch`) without an observed reason in the scenario.
4. The success report is concise: at minimum it states success/failure plus the two counts. No noise about flags the scenario did not require.

## Scenario B — edge (5 items)

1. **[critical]** The plan invokes `uloop compile` with **both** `--force-recompile` enabled **and** `--wait-for-domain-reload` enabled.
2. **[critical]** The argument form matches the documented Examples (e.g., `--force-recompile true --wait-for-domain-reload true`). Either bare-flag form (`--force-recompile`) or value-form is acceptable as long as both options end up enabled.
3. The plan acknowledges that completion blocks until Domain Reload finishes (i.e., the agent will not report back before that).
4. The plan still reports `ErrorCount` / `WarningCount` from the JSON output after the wait.
5. The plan does not invoke unrelated subcommands.

## Scenario C — edge (5 items)

1. **[critical]** The plan invokes `uloop fix` to remove stale lock files (`compiling.lock` / `domainreload.lock` / `serverstarting.lock`) from the project's `Temp/` directory.
2. **[critical]** After `uloop fix` succeeds, the plan retries `uloop compile`.
3. The plan explains to the user what happened (stale lock files were removed and compile retried).
4. The plan does **not** suggest restarting Unity (`uloop launch -r`) as the first response — `uloop fix` is the correct response per the skill's Troubleshooting section.
5. The plan reports `ErrorCount` / `WarningCount` after the retry succeeds.

## Scenario H — hold-out (5 items, evaluated at convergence only)

1. **[critical]** The plan recognizes that Unity Editor process is not running (i.e., it does NOT default to `uloop fix` first — that fixes locks, not absence of process).
2. **[critical]** The plan invokes `uloop launch -r` to start Unity.
3. After Unity starts, the plan retries `uloop compile`.
4. The plan reports the situation to the user ("Unity was not running, started it, then completed compile").
5. The plan does not silently fail; it visibly handles the no-process case.
