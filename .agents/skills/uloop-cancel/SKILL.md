---
name: uloop-cancel
description: "Cancel the uloop command the Unity Editor for this project is running now. Use when BUSY keeps rejecting commands or your own command does not finish."
---

# uloop cancel

Cancel the uloop command that holds the Editor's command slot, from another terminal or after the waiting command was given up on.

## When to Use

- Every command is rejected with `UNITY_SERVER_BUSY` and the running command shows no sign of finishing.
- A command you started does not return, for example an `execute-dynamic-code` snippet that awaits something that never completes.

## Usage

```bash
uloop cancel
uloop cancel --project-path <path>
```

It takes no other options. It answers while Unity is busy and while a command blocks Unity's main thread.

## Output

Returns JSON with:

- `Outcome`: what the cancel found
- `RunningToolName`, `RunningToolElapsedSeconds`: the command that held the slot; `null` for `NothingRunning`
- `Success`: `false` only for `NotCancellable`

| Outcome | Meaning | Exit code |
|---------|---------|-----------|
| `Cancelled` | The running command was asked to stop | 0 |
| `NothingRunning` | No command held the slot | 0 |
| `NotCancellable` | The running command cannot be cancelled and keeps running | 1 |

When Unity cannot be reached, the error goes to stderr, nothing goes to stdout, and it exits 1. A Unity package too old to support `uloop cancel` answers with a `UNITY_RPC_ERROR` naming the unknown command.

## What Cannot Be Cancelled

`NotCancellable` covers only these:

- `compile` that waits for the domain reload (the default)
- `run-tests --test-mode PlayMode --respect-enter-play-mode-settings`
- a tool started by code inside the Editor (an extension or an Editor window), not by the CLI

A default EditMode `run-tests` and `compile --no-wait-for-domain-reload` can be cancelled.

## What the Waiting Command Gets

- Most commands fail with `ErrorCode` `TOOL_CANCELLED` (not retryable).
- `execute-dynamic-code` returns its usual cancelled result: `Success: false` with "Execution was cancelled or timed out".

`Cancelled` also resumes Play Mode when the Editor is paused, whether a pause point or a manual pause holds it — the same as when the waiting CLI is killed.

## After Cancelling

Run `uloop status` and wait for `Ready` before the next command. `Cancelled` means the command was asked to stop, not that it already stopped:

- A tool that ignores its cancellation token keeps the slot until it returns on its own.
- `execute-dynamic-code` returns at once and frees the slot, but a snippet that ignores `ct` keeps running in the background.
- While a command blocks Unity's main thread, `execute-dynamic-code` cannot return; its slot is freed about 5 seconds later, when another command arrives.
