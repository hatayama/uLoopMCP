---
name: uloop-execute-dynamic-code
description: "Execute C# code dynamically in Unity Editor. Use when you need to: (1) Wire prefab/material references and AddComponent operations, (2) Edit SerializedObject properties and reference wiring, (3) Perform scene/hierarchy edits and batch operations, (4) PlayMode automation (click buttons, raycast, invoke methods), (5) PlayMode UI controls (InputField, Slider, Toggle, Dropdown), (6) PlayMode inspection (scene info, reflection, physics state). NOT for file I/O or script authoring."
context: fork
---

# Task

Execute the following request using `uloop execute-dynamic-code`: $ARGUMENTS

## Workflow

1. Read the relevant reference file(s) from the Code Examples section below
2. Construct C# code based on the reference examples
3. Execute via Bash: `uloop execute-dynamic-code --code '<code>'`
4. If execution fails, adjust code and retry
5. Report the execution result

## Code Rules

Write direct statements only — no class/namespace/method wrappers. Return is optional.

```csharp
using UnityEngine;
var x = Mathf.PI;
return x;
```

**Shell quoting**: bash/zsh uses single quotes (`'Debug.Log("Hello!");'`), PowerShell doubles inner quotes (`'Debug.Log(""Hello!"");'`).

**Forbidden** — these will be rejected at compile time: `System.IO.*`, `AssetDatabase.CreateFolder`, creating/editing `.cs`/`.asmdef` files. Use terminal commands for file operations instead.

## Code Examples by Category

For detailed code examples, refer to these files:

- **Prefab operations**: See [references/prefab-operations.md](references/prefab-operations.md)
  - Create prefabs, instantiate, add components, modify properties
- **Material operations**: See [references/material-operations.md](references/material-operations.md)
  - Create materials, set shaders/textures, modify properties
- **Asset operations**: See [references/asset-operations.md](references/asset-operations.md)
  - Find/search assets, duplicate, move, rename, load
- **ScriptableObject**: See [references/scriptableobject.md](references/scriptableobject.md)
  - Create ScriptableObjects, modify with SerializedObject
- **Scene operations**: See [references/scene-operations.md](references/scene-operations.md)
  - Create/modify GameObjects, set parents, wire references, load scenes
- **Batch operations**: See [references/batch-operations.md](references/batch-operations.md)
  - Bulk modify objects, batch add/remove components, rename, layer/tag/material replacement
- **Cleanup operations**: See [references/cleanup-operations.md](references/cleanup-operations.md)
  - Detect broken scripts, missing references, unused materials, empty GameObjects
- **Undo operations**: See [references/undo-operations.md](references/undo-operations.md)
  - Undo-aware operations: RecordObject, AddComponent, SetParent, grouping
- **Selection operations**: See [references/selection-operations.md](references/selection-operations.md)
  - Get/set selection, multi-select, filter by type/editability
- **PlayMode automation**: See [references/playmode-automation.md](references/playmode-automation.md)
  - Click UI buttons, raycast interaction, invoke methods, set fields, tool combination workflows
- **PlayMode UI controls**: See [references/playmode-ui-controls.md](references/playmode-ui-controls.md)
  - InputField, Slider, Toggle, Dropdown, drag & drop simulation, list all UI controls
- **PlayMode inspection**: See [references/playmode-inspection.md](references/playmode-inspection.md)
  - Scene info, game state via reflection, physics state, GameObject search, position/rotation
