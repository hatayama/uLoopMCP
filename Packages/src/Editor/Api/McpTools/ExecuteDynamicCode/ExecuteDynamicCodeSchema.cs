using System.Collections.Generic;
using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Parameter schema for dynamic code execution tool
    /// Related classes: ExecuteDynamicCodeTool, ExecuteDynamicCodeResponse
    /// </summary>
    public class ExecuteDynamicCodeSchema : BaseToolSchema
    {
        /// <summary>C# code to execute</summary>
        [Description(@"Editor automation only — no file I/O, no script authoring.

Direct statements only (no classes/namespaces/methods); return is optional (auto 'return null;' if omitted).

You may include using directives at the top; they are hoisted above the wrapper.
Example:
  using UnityEngine;
  var x = Mathf.PI;
  return x;

Do:
- Prefab/material wiring (PrefabUtility)
- AddComponent + reference wiring (SerializedObject)
- Scene/hierarchy edits

Don’t:
- System.IO.* (File/Directory/Path)
- AssetDatabase.CreateFolder / file writes
- Create/edit .cs/.asmdef (use Terminal/IDE instead)

Need files/dirs? Run terminal commands.")]
        public string Code { get; set; } = "";
        
        /// <summary>Runtime parameters (advanced; usually unnecessary)</summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [Description(@"Advanced, optional parameters passed to the snippet.

- For throwaway snippets, prefer defining locals directly in code
- Keys are not preserved; values map to parameters[""param0""], parameters[""param1""], ...
- Use when injecting UnityEngine.Object or sensitive values, or reusing a snippet with varying data
- Use object values supported by the Editor; prefer fully-qualified type names for ambiguous refs (e.g., UnityEngine.Object)

- Input type must be an OBJECT. Strings like ""{}"" are invalid.
- Do: omit 'Parameters' entirely, or use {}
- Don't: ""{}"" (string), ""{""a"":1}"" (string)

Examples:
OK:   ""Parameters"": {}
OK:   (omit the 'Parameters' property)
NG:   ""Parameters"": ""{}""")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        /// <summary>Compile only (do not execute)</summary>
        [Description(@"Compile only (no execution).

- Uses Roslyn validation to surface diagnostics
- For new MonoBehaviours: create .cs → compile (mcp__uLoopMCP__compile with ForceRecompile=false) → ensure ErrorCount=0 → AddComponent")]
        public bool CompileOnly { get; set; } = false;

        /// <summary>Attempt to auto-qualify common UnityEngine identifiers once and retry on failure</summary>
        [Description(@"Auto-qualify common UnityEngine identifiers and retry once when compilation fails.

- Behavior: If the snippet lacks 'using UnityEngine;' and errors like CS0103/CS0246 occur for common Unity types,
  the tool inserts 'using UnityEngine;' at the top and retries once. This is a best-effort convenience and won't
  modify your source files.
- Disable if you prefer manual control.")]
        public bool AutoQualifyUnityTypesOnce { get; set; } = false;

        /// <summary>Enable parallel execution mode</summary>
        [Description(@"Enable parallel execution mode (default: false).

When false (default): Exclusive execution - safe, sequential processing.
When true: Allows concurrent execution - faster but requires coordination.

⚠️ CAUTION:
- Parallel executions on the same GameObjects may cause race conditions
- Each execution creates its own Undo group
- Use only for independent operations (e.g., creating different objects in different locations)

Recommended for:
- Independent read-only operations
- Creating objects that don't interact with each other
- Batch operations on separate assets")]
        public bool AllowParallel { get; set; } = false;

        /// <summary>Fire-and-forget mode</summary>
        [Description(@"Fire-and-forget mode (default: false).

When true: Returns immediately after compile succeeds.
Execution continues in Unity background - results/errors not returned to CLI.

Use with async code that doesn't need immediate result feedback.

Behavior:
- Compile errors ARE returned (compile check happens before return)
- Runtime errors logged to Unity Console only (use 'uloop get-logs --search-text FireAndForget')

Use cases:
- Long-running monitoring tasks (wait for button to appear)
- Scheduled actions (wait N seconds then do X)
- Multiple independent background tasks")]
        public bool FireAndForget { get; set; } = false;
    }
}