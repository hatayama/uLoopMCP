# Checklist — uloop-control-play-mode

## Scenario M

- `[critical]` Issue `uloop control-play-mode --action Play` to enter PlayMode
- `[critical]` State whether sequencing semantics (when it is safe to call the next PlayMode-dependent command) are documented
- `[critical]` State whether `--action Play` is documented as idempotent (safe to repeat)
- `[critical]` Identify a wait protocol if the response can return before PlayMode is actually active
- `[critical]` Self-report all unclear points and discretionary fill-ins
