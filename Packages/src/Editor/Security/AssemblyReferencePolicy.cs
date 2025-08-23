using System;
using System.Collections.Generic;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Assembly Reference Policy Based on Security Level
    /// Related Classes: DynamicCodeSecurityLevel, DangerousApiDetector, RoslynCompiler
    /// 
    /// Design Principles:
    /// - Disabled: No Assembly References (Compilation Not Allowed)
    /// - Restricted/FullAccess: All Assembly References Allowed (Compilation Possible)
    /// - Security Checks Performed at Runtime by DangerousApiDetector
    /// </summary>
    public static class AssemblyReferencePolicy
    {
        /// <summary>
        /// Retrieve Assembly Name List Based on Security Level
        /// </summary>
        public static IReadOnlyList<string> GetAssemblies(DynamicCodeSecurityLevel level)
        {
            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: Add Nothing (Compilation Not Allowed)
                    return new List<string>();

                case DynamicCodeSecurityLevel.Restricted:
                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 1 & 2: All Assemblies Referenceable
                    // Security Checks Performed at Runtime
                    return GetAllAvailableAssemblies();

                default:
                    VibeLogger.LogWarning(
                        "assembly_policy_unknown_level",
                        $"Unknown security level: {level}",
                        new { level = level.ToString() },
                        correlationId: McpConstants.GenerateCorrelationId(),
                        humanNote: "Unknown security level encountered",
                        aiTodo: "Review security level enum changes"
                    );
                    return new List<string>();
            }
        }

        /// <summary>
        /// Retrieve All Available Assemblies
        /// </summary>
        private static List<string> GetAllAvailableAssemblies()
        {
            List<string> assemblies = new();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Exclude dynamic assemblies or those with empty locations
                if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
                    continue;

                string assemblyName = assembly.GetName().Name;
                assemblies.Add(assemblyName);
            }

            VibeLogger.LogInfo(
                "assembly_policy_all_assemblies",
                "Generated all available assemblies list",
                new { count = assemblies.Count },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "All assemblies made available for compilation",
                aiTodo: "Monitor assembly usage in dynamic code execution"
            );

            return assemblies;
        }

        /// <summary>
        /// Check if the Specified Assembly is Allowed by Security Level
        /// (Maintained for Backward Compatibility)
        /// </summary>
        public static bool IsAssemblyAllowed(string assemblyName, DynamicCodeSecurityLevel level)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: Allow Nothing
                    return false;

                case DynamicCodeSecurityLevel.Restricted:
                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 1 & 2: Allow Everything
                    return true;

                default:
                    return false;
            }
        }
    }
}