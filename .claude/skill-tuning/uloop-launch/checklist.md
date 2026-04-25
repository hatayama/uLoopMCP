# Checklist — uloop-launch

## L1 (downstream-caller follow-up timing)

- [critical] L1.1 — Answer "yes, you may immediately invoke the next `uloop` command after `uloop launch -r` returns successfully" (no extra polling/sleeping required)
- [critical] L1.2 — Cite `uloop-launch/SKILL.md` (specifically the `## Output` section) as the source — NOT CLAUDE.md, NOT another skill
- L1.3 — Mention that the command blocks during launch (i.e. the wait is inside the command, not the caller's responsibility)
- L1.4 — Mention what to do on timeout / non-zero exit (treat as failure, do not assume Unity is up)

## L2 (long-running launch behavior)

- [critical] L2.1 — Answer "the command itself runs an internal startup probe; the wait is by design"
- L2.2 — Cite the same `## Output` paragraph
- L2.3 — Avoid recommending the user kill and retry as the first response

## Self-containment audit (cross-cutting)

- [critical] SC.1 — In `Discretionary fill-ins`, the subagent must NOT cite CLAUDE.md, the global build rule, or another skill as the source of the "no extra wait needed" answer. If they do, mark it as a self-containment failure regardless of accuracy.
