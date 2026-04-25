# Priority matrix vs measured gap-discovery — wrap

Post-rollout validation of `priority-matrix.md` after all 16 bundled skills were tuned across 4 batches. This file captures: (a) where the matrix predicted well, (b) where it missed, and (c) refinements proposed for the next time the matrix is regenerated.

## Coverage tally

16/16 bundled skills have at minimum `iter-00-static.md` + `scenarios.md` + `checklist.md` + `ledger.md` + `iter-01.md` under `.claude/skill-tuning/<skill>/`. SK additionally has `iter-02.md` (the only skill in the rollout that needed a substantive Iter 2 patch).

| Batch | Skills | Convergence iter | Iter 2 needed |
|-------|--------|------------------|---------------|
| 1 | EDC, simulate-mouse-demo, control-play-mode | Iter 1+ | — |
| 2 | screenshot, run-tests, get-logs, find-game-objects | Iter 1+ | — |
| 3 | execute-menu-item, clear-console, get-hierarchy, simulate-keyboard | Iter 1–2 | **simulate-keyboard** (SK_wait) |
| 4 | focus-window, record-input, replay-input, simulate-mouse-input, simulate-mouse-ui | **Iter 1** (all 5) | none |

## Where the matrix predicted well

1. **`focus-window` (rank 18, score 1)**: matrix predicted minimal work. Measured: converged at Iter 1 with zero fabrications, only the formulaic Iter-0 description+output patches. Correct deferral candidate.
2. **`uloop-execute-dynamic-code` (rank 1, score 7)**: matrix predicted highest leverage. Measured: pilot took the most iterations of any skill in the rollout. Score-to-cost correlation strong at the top of the matrix.
3. **`uloop-control-play-mode` (rank 2, score 6)**: matrix flagged "PlayMode timing semantics" as the risk. Measured: this was indeed where the patches landed (async PlayMode caveat addition during retro-sync per session summary).

## Where the matrix missed

### Miss 1 — `uloop-get-hierarchy`: substantive correctness bug
- **Predicted**: rank 8-tied, score 4 (just a description gap)
- **Measured**: substantive correctness bug found during Iter 0 reading of `GetHierarchyResponse.cs` — both description AND body claimed inline JSON, but the response is `{ message, hierarchyFilePath }` with the actual data in a file on disk. Required a description rewrite, not a "How" suffix append.
- **Root cause**: the matrix's description-compliance axis only tracks "How" presence, not "How accuracy". A description that LIES about behavior scores the same as one that's terse but correct.

### Miss 2 — `simulate-mouse-input` and `simulate-mouse-ui`: missing-section worse than stub
- **Predicted**: rank 14-tied, score 3 (modest body complexity, description fully compliant)
- **Measured**: both bodies were MISSING `## Output` entirely — the worst-case sub-pattern in the response-shape stub family. Same severity as the SK case from batch 3.
- **Root cause**: the matrix scored body completeness implicitly via line count, but a skill can be 100 lines and still have a critical zero-line section. Section-level checklist would catch this.

### Miss 3 — `uloop-simulate-keyboard` SK_wait
- **Predicted**: rank 8-tied, score 4 ("hold-key semantics implicit")
- **Measured**: matrix correctly flagged the family but missed the specific symptom — the example sequence in the body had no documented wait primitive between KeyDown and KeyUp, forcing the agent to invent `sleep N`. Required Iter 2 to patch.
- **Root cause**: example-vs-narrative drift is its own pattern. When the example shows N steps but the narrative documents fewer mechanisms, agents fill in.

## Refinements for the next matrix

Add a fourth axis and refine an existing one:

| New / refined axis | Description | Score |
|---------------------|-------------|-------|
| **(refined) Description compliance gap** | Now includes "How accuracy" not just "How presence". A misleading "How" is more dangerous than a missing one. | 0 (compliant + accurate) → 3 (absent OR misleading) |
| **(new) Body section completeness** | Are all of `## Usage`, `## Parameters`, `## Output`, `## Examples` present and non-stub? Missing `## Output` is worse than a stub, which is worse than a complete one. | 0 (all complete) → 3 (one or more sections missing) |
| **(new) Example-narrative coupling** | Does every primitive used in `## Examples` appear in `## Parameters` or in narrative rules? `sleep N` between commands without a documented timing rule = drift. | 0 (no drift) → 3 (multiple drifts) |

If these axes had been applied originally:
- get-hierarchy would have moved from rank 8 to rank ~3 (description accuracy = 3)
- simulate-mouse-input/ui would have moved from rank 14 to rank ~5 (body section completeness = 3, missing `## Output`)
- simulate-keyboard would have moved from rank 8 to rank ~5 (example-narrative coupling = 2 for the wait gap)

## Measured gap-discovery rate

Across 16 skills:
- **Iter 0 patches (description "How")**: 14/16 needed (only SMI1 had implicit How via "Injects directly into Mouse.current"; one other was already compliant)
- **Iter 0 patches (`## Output` expansion)**: 11/16 needed (5 already had complete output sections)
- **Iter 0 patches (substantive correctness)**: 1/16 needed (get-hierarchy file-path indirection)
- **Iter 1 closed-book PASS rate (critical)**: 16/16
- **Iter 2 patches needed**: 1/16 (simulate-keyboard SK_wait)
- **Discretionary fill-ins surfaced by closed-book agents but NOT triggering Iter 2**: 1 (record-input wait timing — agent correctly deferred to user instead of guessing, no patch needed)

**Cost per skill** (median): 1 working session for Iter 0 + Iter 1 + artifacts. Outliers: EDC (~2 sessions), simulate-keyboard (~1.5 sessions due to Iter 2).

## Conclusion

The matrix was directionally correct at the extremes (top rank → highest cost, bottom rank → trivial) but misranked the middle by ~5 positions. The two refinements above (description ACCURACY, body section completeness) would close most of the gap. The `## Output` stub family was universal enough that a single batch-wide grep on `## Output\n\n[A-Z][^\n]{,80}\n` could have triaged the entire rollout in 30 seconds without any matrix scoring at all.

For the next time this methodology is applied (new bundled skills added):
1. Run the section-presence + accuracy-check linter first as cheap triage
2. Reserve the matrix for tie-breaking among skills that pass triage
3. Always inspect the corresponding `*Response.cs` during Iter 0 — that's how the get-hierarchy correctness bug was caught
