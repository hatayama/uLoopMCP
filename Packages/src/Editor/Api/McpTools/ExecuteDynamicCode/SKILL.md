---
name: uloop-execute-dynamic-code
description: Execute C# code dynamically in Unity Editor via uloop CLI. Use for editor automation: (1) Prefab/material wiring and AddComponent operations, (2) Reference wiring with SerializedObject, (3) Scene/hierarchy edits and batch operations. NOT for file I/O or script authoring.
---

# uloop execute-dynamic-code

Execute C# code dynamically in Unity Editor.

## Usage

```bash
uloop execute-dynamic-code --code '<c# code>'
```

## Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `--code` | string | C# code to execute (direct statements, no class wrapper) |
| `--compile-only` | boolean | Compile without execution |
| `--auto-qualify-unity-types-once` | boolean | Auto-qualify Unity types |
| `--allow-parallel` | boolean | Enable parallel execution (⚠️ use with caution) |

## Code Format

Write direct statements only (no classes/namespaces/methods). Return is optional.

```csharp
// Using directives at top are hoisted
using UnityEngine;
var x = Mathf.PI;
return x;
```

## String Literals (Shell-specific)

| Shell | Method |
|-------|--------|
| bash/zsh/MINGW64/Git Bash | `'Debug.Log("Hello!");'` |
| PowerShell | `'Debug.Log(""Hello!"");'` |

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

### bash / zsh / MINGW64 / Git Bash

```bash
uloop execute-dynamic-code --code 'return Selection.activeGameObject?.name;'
uloop execute-dynamic-code --code 'new GameObject("MyObject");'
uloop execute-dynamic-code --code 'UnityEngine.Debug.Log("Hello from CLI!");'
```

### PowerShell

```powershell
uloop execute-dynamic-code --code 'return Selection.activeGameObject?.name;'
uloop execute-dynamic-code --code 'new GameObject(""MyObject"");'
uloop execute-dynamic-code --code 'UnityEngine.Debug.Log(""Hello from CLI!"");'
```

## Output

Returns JSON with execution result or compile errors.

## Notes

For file/directory operations, use terminal commands instead.

## Code Examples by Category

For detailed code examples, refer to these files:

- **Prefab operations**: See [examples/prefab-operations.md](examples/prefab-operations.md)
  - Create prefabs, instantiate, add components, modify properties
- **Material operations**: See [examples/material-operations.md](examples/material-operations.md)
  - Create materials, set shaders/textures, modify properties
- **Asset operations**: See [examples/asset-operations.md](examples/asset-operations.md)
  - Find/search assets, duplicate, move, rename, load
- **ScriptableObject**: See [examples/scriptableobject.md](examples/scriptableobject.md)
  - Create ScriptableObjects, modify with SerializedObject
- **Scene operations**: See [examples/scene-operations.md](examples/scene-operations.md)
  - Create/modify GameObjects, set parents, wire references, load scenes

## Parallel Execution Mode

`--allow-parallel` enables concurrent execution of multiple requests.

```bash
uloop execute-dynamic-code --code 'new GameObject("Object1");' --allow-parallel
```

### ⚠️ Risks

- **Race conditions**: Simultaneous modifications to the same GameObject may cause unpredictable behavior
- **Undo complexity**: Each parallel execution creates its own Undo group
- **Unity API thread safety**: Some Unity APIs are not thread-safe

### When to Use

- Independent operations (e.g., creating objects in different locations)
- Non-overlapping asset modifications
- Read-only operations (queries, searches)

### When NOT to Use

- Modifying the same GameObject or component from multiple executions
- Operations with sequential dependencies
- Operations that require consistent Editor state
