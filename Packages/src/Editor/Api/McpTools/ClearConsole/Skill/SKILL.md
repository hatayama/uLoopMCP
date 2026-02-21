---
name: uloop-clear-console
description: "Clear Unity console logs. Use when: clearing console before tests, starting fresh debugging session, or when user asks to clear logs. Removes all log entries from Unity Console."
---

# uloop clear-console

Clear Unity console logs.

## Usage

```bash
uloop clear-console [--add-confirmation-message]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--add-confirmation-message` | boolean | `false` | Add confirmation message after clearing |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project. Path resolution follows the same rules as `cd` — absolute paths are used as-is, relative paths are resolved from cwd. |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Examples

```bash
# Clear console
uloop clear-console

# Clear with confirmation
uloop clear-console --add-confirmation-message
```

## Output

Returns JSON confirming the console was cleared.
