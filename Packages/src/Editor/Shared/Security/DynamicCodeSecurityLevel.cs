namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Security level definitions for ExecuteDynamicCodeTool
    /// Related classes: DynamicCodeSecurityManager, AssemblyReferencePolicy, RoslynCompiler
    /// </summary>
    public enum DynamicCodeSecurityLevel
    {
        /// <summary>
        /// Level 0: Complete Disabled
        /// - No assembly references are added
        /// - Compilation is NOT allowed
        /// - Execution is NOT allowed
        /// - Use case: Production environments, maximum security priority
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Level 1: Restricted Execution (Recommended)
        /// - Basic .NET assemblies (mscorlib, System, netstandard)
        /// - Official Unity API assemblies (UnityEngine, UnityEditor, etc.)
        /// - Dangerous APIs (System.IO, System.Net.Http, reflection, etc.) are blocked
        /// - Use case: Standard Unity development, safety-focused
        /// </summary>
        Restricted = 1,

        /// <summary>
        /// Level 2: Full Access
        /// - All assemblies available (no restrictions)
        /// - Includes System.Reflection.Emit, System.CodeDom
        /// - Use case: Advanced development, system integration, debugging, dynamic code generation
        /// - Warning: Security risks present - use only with trusted code
        /// </summary>
        FullAccess = 2
    }
}