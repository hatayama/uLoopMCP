# Scenarios — uloop-launch

Scope of this tuning round: verify that the SKILL.md communicates the **blocking-until-ready** semantics to a downstream caller. Other launch behaviors (Unity Hub registration, build target switching) are out of scope and re-use Iter 0 description audit only.

Mode: **plan-and-describe** (subagent does not invoke `uloop`; reports the command plan + observable assumptions).

## L1 — median (downstream-caller follow-up timing)

**User intent**: "Unity Editor が起動してへんかった。`uloop launch -r` で起動したあと、すぐ `uloop compile` 叩いてええんやろうか? それとも何秒か待つべき?"

The scenario directly probes the gap that hold-out H of `uloop-compile` surfaced.

## L2 — edge (long-running launch)

**User intent**: "`uloop launch -r` がなかなか終わらへん。中で何待っとるんや?"

Scenario probes whether the SKILL.md communicates that the command itself is doing the wait (vs. the user needing to retry separately).

## Scope guard

- This tuning round does NOT re-test the description's three When clauses (1) version match, (2) restart, (3) build target — those passed Iter 0 static check unchanged.
- This tuning round does NOT add a hold-out scenario. Rationale: the `uloop launch` change is a 1-line addition under `## Output`, not a structural revision; the existing iter-04.md from `uloop-compile` already serves as the cross-skill hold-out validation source.
