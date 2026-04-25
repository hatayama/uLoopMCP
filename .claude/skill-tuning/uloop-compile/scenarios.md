# Scenarios — uloop-compile

Frozen at Iter 1 baseline. Do not modify in response to fixes.

## Execution mode

Subagents **plan and describe** the action; they **do not invoke `uloop` for real** (no Unity dependency, repeatable, fast).
The deliverable is "planned command sequence + reasoning + how I would interpret the expected output", evaluated against `checklist.md`.

This deviates from mizchi's default ("actually execute") because uloop requires a running Unity Editor and produces non-deterministic side effects. Plan-and-describe still surfaces description-level and body-level ambiguities, which is the primary goal.

## Scenario A — median

**Context**: You just edited a single C# file in this Unity project (added a new field to a MonoBehaviour). You want to confirm it compiles cleanly before continuing.

## Scenario B — edge

**Context**: You changed a `[InitializeOnLoad]`-style class that affects the editor at startup. A normal compile won't apply the change because it's a domain-reload-triggering change. You need to force a full recompilation **and wait until Domain Reload completes** before reporting back. Otherwise the user will follow up with stale state.

## Scenario C — edge

**Context**: You ran `uloop compile` and the CLI returned an error mentioning "Unity is busy" or refused to connect. You suspect a previous Unity session left stale lock files in the project's `Temp/` directory.

## Scenario H — hold-out (reserved for convergence judgment)

**Context**: You want to compile, but `uloop compile` returns a connection failure. Investigation shows the Unity Editor process is not running at all (no stale lock — Unity is simply down). You need to bring up Unity and then complete the compile task the user originally asked for.

**Reservation rule**: Do not execute scenario H during normal iterations. Use it only at convergence to detect overfitting.
