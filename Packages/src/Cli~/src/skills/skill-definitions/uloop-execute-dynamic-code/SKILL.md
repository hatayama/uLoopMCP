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

## IMPORTANT: String Literals (OS/Shell-specific)

The CLI converts backticks to double quotes: `` `Hello` `` becomes `"Hello"`.

### Mac (bash/zsh)

Use single quotes to wrap, double quotes inside:

```bash
uloop execute-dynamic-code --code 'Debug.Log("Hello World");'
```

### Windows (MINGW64 / Git Bash / cmd)

Use backticks for C# strings:

```bash
uloop execute-dynamic-code --code "Debug.Log(\`Hello World\`);"
```

### Windows (PowerShell)

Use `""` for C# strings:

```powershell
uloop execute-dynamic-code --code 'Debug.Log(""Hello World"");'
```

### Summary

| OS | Shell | Method |
|----|-------|--------|
| Mac | bash/zsh | `'Debug.Log("Hello");'` |
| Windows | MINGW64/Git Bash/cmd | `` Debug.Log(`Hello`) `` |
| Windows | PowerShell | `Debug.Log(""Hello"")` |

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

### Mac (bash/zsh)

```bash
# Get selected GameObject name
uloop execute-dynamic-code --code 'return Selection.activeGameObject?.name;'

# Create empty GameObject
uloop execute-dynamic-code --code 'new GameObject("MyObject");'

# Log a message
uloop execute-dynamic-code --code 'UnityEngine.Debug.Log("Hello from CLI");'

# Add component to selected
uloop execute-dynamic-code --code 'Selection.activeGameObject.AddComponent<Rigidbody>();'
```

### Windows (MINGW64 / Git Bash / cmd)

```bash
# Get selected GameObject name
uloop execute-dynamic-code --code "return Selection.activeGameObject?.name;"

# Create empty GameObject (use backticks)
uloop execute-dynamic-code --code "new GameObject(\`MyObject\`);"

# Log a message (use backticks)
uloop execute-dynamic-code --code "UnityEngine.Debug.Log(\`Hello from CLI\`);"

# Add component to selected
uloop execute-dynamic-code --code "Selection.activeGameObject.AddComponent<Rigidbody>();"
```

### Windows (PowerShell)

```powershell
# Get selected GameObject name
uloop execute-dynamic-code --code 'return Selection.activeGameObject?.name;'

# Create empty GameObject (use "")
uloop execute-dynamic-code --code 'new GameObject(""MyObject"");'

# Log a message (use "")
uloop execute-dynamic-code --code 'UnityEngine.Debug.Log(""Hello from CLI"");'

# Add component to selected
uloop execute-dynamic-code --code 'Selection.activeGameObject.AddComponent<Rigidbody>();'
```

## Output

Returns JSON with execution result or compile errors.

## Notes

For file/directory operations, use terminal commands instead.
