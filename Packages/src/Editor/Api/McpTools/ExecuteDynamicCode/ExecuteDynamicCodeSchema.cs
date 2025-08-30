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
        [Description(@"Editor automation C# snippet.

- Direct statements only (no classes/namespaces/methods)
- Must return a value (e.g., return ""Done"";)
- You may include using directives at the top; they are hoisted above the generated wrapper.
  Example:
    using UnityEngine;
    var x = Mathf.PI;
    return x;
- Valid: GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); return ""Created"";
- Invalid: GameObject.CreatePrimitive(PrimitiveType.Cube); // missing return

- For any file/directory or other I/O operations, do NOT use this tool; run normal commands instead.")]
        public string Code { get; set; } = "";
        
        /// <summary>Runtime parameters</summary>
        [Description(@"Runtime parameters passed to the snippet.

- Use object values supported by the Editor
- Use fully-qualified type names for ambiguous refs (e.g., UnityEngine.Object)
- Prefer explicit setup inside code over relying on parameter ordering")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        /// <summary>Compile only (do not execute)</summary>
        [Description(@"Compile only (no execution).

- Uses Roslyn validation to surface diagnostics
- For new MonoBehaviours: create .cs → compile (mcp__uLoopMCP__compile with ForceRecompile=false) → ensure ErrorCount=0 → AddComponent")]
        public bool CompileOnly { get; set; } = false;
    }
}