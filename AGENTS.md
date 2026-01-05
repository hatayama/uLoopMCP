Comments in the code, commit messages, and PR titles and bodies should be written in English.

Always use `io.github.hatayama.uLoopMCP` as the namespace.

## Constants and Hardcoding

Avoid hardcoding values like package names, paths, or configuration strings directly in the code. Instead, define them as constants in `McpConstants.cs` and reference them from there.

For package-related information, use the dynamic properties provided in `McpConstants`:
- `McpConstants.PackageName` - Package name (e.g., `io.github.hatayama.uloopmcp`)
- `McpConstants.PackageAssetPath` - Unity asset path for `AssetDatabase.LoadAssetAtPath()`
- `McpConstants.PackageResolvedPath` - File system path for file operations
- `McpConstants.PackageNamePattern` - Glob pattern for directory searching (e.g., `io.github.hatayama.uloopmcp@*`)
- `McpConstants.PackageNamespace` - C# namespace (e.g., `io.github.hatayama.uLoopMCP`)
