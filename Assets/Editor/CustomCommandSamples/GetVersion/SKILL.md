---
name: uloop-get-version
description: Get uLoopMCP version information via uloop CLI. Use when you need to check uLoopMCP package version, verify installation, or troubleshoot version compatibility.
internal: true
---

# uloop get-version

Get Unity version and project information.

## Usage

```bash
uloop get-version
```

## Parameters

None.

## Output

Returns JSON with:
- `UnityVersion`: Unity Editor version
- `Platform`: Current platform
- `DataPath`: Assets folder path
- `PersistentDataPath`: Persistent data path
- `IsEditor`: Whether running in editor
- `ProductName`: Application product name
- `CompanyName`: Company name
- `Version`: Application version

## Notes

This is a sample custom tool demonstrating how to create MCP tools.
