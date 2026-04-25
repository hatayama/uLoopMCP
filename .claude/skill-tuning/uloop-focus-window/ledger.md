# Failure pattern ledger — uloop-focus-window

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| FW1: description lacked "How" suffix | Iter 0 | Iter 0 | Appended OS-API-via-launch-unity How explanation, including the "bypasses TCP server" bonus that explains the unique "works during compile/reload" property. |
| FW2: response-shape stub `## Output` | Iter 0 | Iter 0 | Expanded one-line stub to enumerate exactly the two fields `focus-window.ts` actually emits (`Success`, `Message`), with example Message strings and an explicit "no PID/handle/platform" disclaimer. |

## Carryover (informational)
- Same response-shape-stub family as EM/CC/GH/GL/FG/SK. Standard fix continues to be effective.
- This skill is the simplest case in the family: only two fields, no complex enums or nested objects.
