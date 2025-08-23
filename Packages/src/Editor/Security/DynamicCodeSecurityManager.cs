using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Security management utility for ExecuteDynamicCodeTool
    /// Related Classes: DynamicCodeSecurityLevel, AssemblyReferencePolicy, RoslynCompiler
    /// </summary>
    public static class DynamicCodeSecurityManager
    {
        /// <summary>
        /// Check if code execution is possible at the specified security level
        /// </summary>
        public static bool CanExecute(DynamicCodeSecurityLevel level)
        {
            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: Execution completely prohibited
                    VibeLogger.LogWarning(
                        "security_execution_blocked",
                        "Execution blocked at Disabled security level",
                        new { level = level.ToString() },
                        correlationId: McpConstants.GenerateCorrelationId(),
                        humanNote: "Code execution prevented by security policy",
                        aiTodo: "Track execution attempts at disabled level"
                    );
                    return false;

                case DynamicCodeSecurityLevel.Restricted:
                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 1, 2: Execution permitted
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Retrieve list of allowed assemblies based on the security level
        /// </summary>
        public static IReadOnlyList<string> GetAllowedAssemblies(DynamicCodeSecurityLevel level)
        {
            return AssemblyReferencePolicy.GetAssemblies(level);
        }
    }
}