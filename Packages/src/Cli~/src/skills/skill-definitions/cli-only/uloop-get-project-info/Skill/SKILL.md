---
name: uloop-get-project-info
description: "Get Unity project information including version, build target, and platform details. Use when you need to: (1) Check what Unity version a project uses, (2) Get project settings and build target info, (3) Retrieve project metadata for diagnostics or environment checks."
internal: true
---

# uloop get-project-info

Get detailed Unity project information.

## Usage

```bash
uloop get-project-info
```

## Parameters

None.

## Examples

```bash
# Get project info
uloop get-project-info
```

## Output

Returns JSON with project information:

```json
{
  "ProjectName": "MyGame",
  "UnityVersion": "2022.3.1f1",
  "Platform": "StandaloneOSX",
  "CompanyName": "MyCompany",
  "Version": "1.0.0",
  "DataPath": "/path/to/project/Assets",
  "IsPlaying": false,
  "SystemMemorySize": 16384
}
```
