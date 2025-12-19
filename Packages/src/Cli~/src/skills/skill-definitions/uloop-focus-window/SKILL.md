---
name: uloop-focus-window
description: Bring Unity Editor window to front via uloop CLI. Use when you need to focus Unity Editor before capturing screenshots, checking visual state, or ensuring Unity is visible.
---

# uloop focus-window

Bring Unity Editor window to front.

## Usage

```bash
uloop focus-window
```

## Parameters

None.

## Examples

```bash
# Focus Unity Editor
uloop focus-window
```

## Output

Returns JSON confirming the window was focused.

## Notes

- Useful before `uloop capture-gameview` to ensure Game View is visible
- Brings the main Unity Editor window to the foreground
