# Failure pattern ledger — uloop-compile

Per-target ledger. Patterns from other skills (e.g., uloop-screenshot) must NOT be merged here.

Format: each entry has Pattern name, Example issue, General Fix Rule, Seen in (iter list).

## Patterns

- **environment-leak self-containment gap**
  - Example issue: hold-out scenario H subagent (`ab6e6c7c3368e0468`, Iter 3) sourced `uloop launch -r` knowledge from `CLAUDE.md` instead of `uloop-compile/SKILL.md`. Critical items passed only because of environment leak.
  - General Fix Rule: A skill must satisfy its own `[critical]` checklist using only its body. Treat any "I knew this from CLAUDE.md / sibling skill" admission in a subagent's `Discretionary fill-ins` as evidence of a self-containment gap, even when the scenario passes accuracy. Inline the minimum recovery snippet in the skill, do not rely on environment context.
  - Seen in: iter 3
  - Fix landed in: iter 4 (Troubleshooting section split into two failure modes with `uloop launch -r` inlined)
