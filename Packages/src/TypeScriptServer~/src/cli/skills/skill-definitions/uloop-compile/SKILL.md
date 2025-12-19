---
name: uloop-compile
description: Compile Unity project via uloop CLI. Use when you need to verify C# code compiles successfully after editing Unity scripts, before running tests, or to check for compile errors/warnings.
---

# uloop compile

Execute Unity project compilation.

## Usage

```bash
uloop compile [--force-recompile]
```

## Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `--force-recompile` | boolean | Force full recompilation (triggers Domain Reload) |

## Examples

```bash
# Check compilation
uloop compile

# Force full recompilation
uloop compile --force-recompile
```

## Output

Returns JSON:
- `Success`: boolean
- `ErrorCount`: number
- `WarningCount`: number
