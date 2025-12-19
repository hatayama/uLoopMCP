---
name: uloop-capture-gameview
description: Capture Unity Game View as PNG via uloop CLI. Use when you need to take a screenshot of the Game View, capture visual state for debugging, or save game output as an image.
---

# uloop capture-gameview

Capture Unity Game View and save as PNG image.

## Usage

```bash
uloop capture-gameview [--resolution-scale <scale>]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--resolution-scale` | number | `1.0` | Resolution scale (0.1 to 1.0) |

## Examples

```bash
# Capture at full resolution
uloop capture-gameview

# Capture at half resolution
uloop capture-gameview --resolution-scale 0.5
```

## Output

Returns JSON with file path to the saved PNG image.

## Notes

- Use `uloop focus-window` first if needed
- Game View must be visible in Unity Editor
