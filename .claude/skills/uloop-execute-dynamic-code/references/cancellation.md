# Cancellation and Long Awaits

## The `ct` token

The snippet body runs inside a generated method with this signature:

```csharp
public async Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
```

So a `CancellationToken` named `ct` is always in scope; use it without declaring anything.

`ct` is cancelled when:

- the `uloop` CLI that sent the snippet disconnects (for example, its process is killed or interrupted),
- a domain reload starts,
- the Unity Editor quits,
- the snippet was sent with `--yield-to-foreground-requests` and another `execute-dynamic-code` request arrives that it yields to.

## Long awaits block other tool commands

uloop runs one tool command at a time per Editor. While a snippet is awaiting, other tool commands against that Editor (such as `compile`, `get-logs`, or `run-tests`) are rejected as BUSY. An await that may never finish — waiting for an animation or a scripted sequence to complete, or for an external event — keeps the Editor BUSY for as long as it waits. `uloop status` still answers while a tool runs, so use it to check what is running.

## Recommended patterns

Do not add sleeps or polling loops. When you await an API that accepts a cancellation token, pass `ct` to it (`LoadSomethingAsync` stands for any such API):

```csharp
await LoadSomethingAsync(path, ct);
```

With UniTask, attach the token: `await SomeUniTask().AttachExternalCancellation(ct);`, or pass it to APIs that accept one (`cancellationToken: ct`).

When you cannot know whether something will finish, start it and return instead of awaiting it. Let the shell do the waiting, then inspect the result with a follow-up command:

```csharp
using UnityEngine;
GameObject target = GameObject.Find("Target");
target.GetComponent<Animator>().Play("Open");
return "started";
```

With UniTask, call `.Forget()` on a sequence you only want to start.

State that the snippet restores at its end — such as `Time.timeScale` — stays changed if the snippet stops midway. Restore it in a separate command rather than relying on the end of a long snippet:

```csharp
Time.timeScale = 1f;
return Time.timeScale;
```

## What cancellation does to a snippet that ignores `ct`

When `ct` is cancelled, uloop stops waiting for the snippet and frees the Editor for the next command; the response reports `Success: false` with a cancellation `ErrorMessage`. A snippet that never checks `ct` is not stopped, though: its remaining work keeps running in the background. Pass `ct` so the snippet actually stops.
