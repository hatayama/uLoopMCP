---
name: uloop-compile
description: "Compile Unity project. Use when: verifying code compiles after edits, checking for compile errors, or when user asks to compile. Returns error/warning counts."
---

# uloop compile

Execute Unity project compilation.

## Usage

```bash
uloop compile [--force-recompile] [--wait-for-domain-reload]
```

## Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `--force-recompile` | boolean | Force full recompilation (triggers Domain Reload) |
| `--wait-for-domain-reload` | boolean | Wait until Domain Reload completes before returning (effective only with `--force-recompile true`) |

## Examples

```bash
# Check compilation
uloop compile

# Force full recompilation
uloop compile --force-recompile

# Force recompilation and wait for Domain Reload completion
uloop compile --force-recompile true --wait-for-domain-reload true
```

## Output

Returns JSON:
- `Success`: boolean
- `ErrorCount`: number
- `WarningCount`: number

## Troubleshooting

If CLI hangs or shows "Unity is busy" errors after compilation, stale lock files may be preventing connection. Run the following to clean them up:

```bash
uloop fix
```

This removes any leftover lock files (`compiling.lock`, `domainreload.lock`, `serverstarting.lock`) from the Unity project's Temp directory.
