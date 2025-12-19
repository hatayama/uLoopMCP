---
name: uloop-execute-dynamic-code
description: Execute C# code dynamically in Unity Editor via uloop CLI. Use for editor automation like prefab/material wiring, AddComponent, reference wiring with SerializedObject, or scene/hierarchy edits. NOT for file I/O or script authoring.
---

# uloop execute-dynamic-code

Execute C# code dynamically in Unity Editor.

## Usage

```bash
uloop execute-dynamic-code --code "<c# code>"
```

## Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `--code` | string | C# code to execute (direct statements, no class wrapper) |
| `--compile-only` | boolean | Compile without execution |
| `--auto-qualify-unity-types-once` | boolean | Auto-qualify Unity types |

## Code Format

Write direct statements only (no classes/namespaces/methods). Return is optional.

```csharp
// Using directives at top are hoisted
using UnityEngine;
var x = Mathf.PI;
return x;
```

## Allowed Operations

- Prefab/material wiring (PrefabUtility)
- AddComponent + reference wiring (SerializedObject)
- Scene/hierarchy edits
- Inspector modifications

## Forbidden Operations

- System.IO.* (File/Directory/Path)
- AssetDatabase.CreateFolder / file writes
- Create/edit .cs/.asmdef files

## Examples

```bash
# Get selected GameObject name
uloop execute-dynamic-code --code "return Selection.activeGameObject?.name;"

# Create empty GameObject
uloop execute-dynamic-code --code "new GameObject(\"MyObject\");"

# Add component to selected
uloop execute-dynamic-code --code "Selection.activeGameObject.AddComponent<Rigidbody>();"
```

## Output

Returns JSON with execution result or compile errors.

## Notes

For file/directory operations, use terminal commands instead.
