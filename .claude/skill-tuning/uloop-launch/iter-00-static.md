# Iteration 0 — static description ↔ body audit (uloop-launch)

## Inputs

- `.claude/skills/uloop-launch/SKILL.md` (51 lines pre-edit)
- Implementation: `Packages/src/Cli~/src/commands/launch.ts`, `Packages/src/Cli~/src/launch-readiness.ts`
- Cross-skill driver: `uloop-compile` Iter 4 hold-out H surfaced one downstream caller gap

## Static gaps found

| ID | Gap | Severity |
|----|-----|----------|
| LG1 | description's What/When/How structure is compliant (What: "Launch Unity project with matching Editor version via uloop CLI"; When: 3 numbered clauses; How: "via uloop CLI"). No fix needed. | none |
| LG2 | `## Output` section says only "If launching, opens Unity in background" — does NOT say the command blocks on internal readiness probes before returning. The implementation calls `waitForDynamicCodeReadyAfterLaunch` or `waitForLaunchReadyAfterLaunch` synchronously (`launch.ts:94/98`), with a 180s timeout (`launch-readiness.ts:16`). | high (drives downstream-caller race conditions; this is precisely the gap H surfaced in `uloop-compile`'s Iter 4 hold-out) |
| LG3 | `--max-depth`, `-a, --add-unity-hub`, `-f, --favorite` exist in the parameter table but are not reflected in description's When clauses. They are auxiliary modes (Hub registration without launching) that are tangential to the primary "open Unity" job. | low (description should not enumerate every flag) |
| LG4 | No mention of timeout failure mode for the readiness wait (180s). | low (the SKILL.md is for callers, not for SRE; deferred) |

## Iter 0 fix landing

Patched only LG2:

```diff
+The command blocks until Unity is ready to accept further `uloop` requests
+(executes a startup probe internally). When the command returns successfully,
+you may immediately invoke the next `uloop` command (e.g. `uloop compile`)
+without polling or sleeping. On timeout the command exits with a non-zero status.
```

LG3 and LG4 are deferred:
- LG3: 80-point ship — primary use cases are covered by the existing description; flag-level enumeration would bloat without payoff
- LG4: covered indirectly by "On timeout the command exits with a non-zero status" added in the Iter 0 patch — sufficient for a caller skill

## Why no body-level "How" expansion was needed

The pre-existing description already includes "via uloop CLI" suffix. No further description change required.
