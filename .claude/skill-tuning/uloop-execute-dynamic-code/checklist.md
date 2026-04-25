# Checklist — uloop-execute-dynamic-code

Per-scenario [critical] items. Subagent must satisfy all `[critical]` rows using only the inlined SKILL.md.

## Scenario M

- `[critical]` Issue exactly one `uloop execute-dynamic-code --code '...'` invocation
- `[critical]` Wrap inline C# in single quotes (zsh quoting rule from skill)
- `[critical]` Inspect `Success` first to determine outcome
- `[critical]` Document the response field used for runtime error vs compile error triage
- `[critical]` State whether the response field shape (Success/Result/Logs/CompilationErrors/ErrorMessage/UpdatedCode) is documented or guessed
- State whether EditMode-vs-PlayMode targeting is documented
