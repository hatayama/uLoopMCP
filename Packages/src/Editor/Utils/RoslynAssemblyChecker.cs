using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility for checking the existence of Roslyn assemblies
    /// </summary>
    /// <summary>
    /// Utility for checking the existence of Roslyn assemblies
    /// </summary>
    public static class RoslynAssemblyChecker
    {
        private const string ROSLYN_ASSEMBLY_NAME = "Microsoft.CodeAnalysis.CSharp";
        
        /// <summary>
        /// Check if Roslyn assembly is available
        /// </summary>
        public static bool IsRoslynAvailable()
        {
            try
            {
                // Check if the assembly is already loaded
                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                Assembly roslynAssembly = loadedAssemblies.FirstOrDefault(a => 
                    a.GetName().Name == ROSLYN_ASSEMBLY_NAME);
                
                if (roslynAssembly != null)
                {
                    return true;
                }
                
                // Verify type existence (check if assembly is present in the project)
                Type csharpSyntaxType = Type.GetType(
                    "Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree, Microsoft.CodeAnalysis.CSharp",
                    false);
                
                return csharpSyntaxType != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Retrieve version information for the Roslyn assembly
        /// </summary>
        public static string GetRoslynVersion()
        {
            try
            {
                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                Assembly roslynAssembly = loadedAssemblies.FirstOrDefault(a => 
                    a.GetName().Name == ROSLYN_ASSEMBLY_NAME);
                
                if (roslynAssembly != null)
                {
                    return roslynAssembly.GetName().Version.ToString();
                }
                
                return "Not Installed";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }
        
        /// <summary>
        /// Generate installation instructions message
        /// </summary>
        public static string GetInstallationMessage()
        {
            return $"Microsoft.CodeAnalysis.CSharp is required for Roslyn features.\n\n" +
                   "Please install it using OpenUPM:\n" +
                   "1. Open Project Settings → Package Manager\n" +
                   "2. Add OpenUPM as Scoped Registry:\n" +
                   "   - Name: OpenUPM\n" +
                   "   - URL: https://package.openupm.com\n" +
                   "   - Scope(s): org.nuget\n" +
                   "3. Open Window → Package Manager\n" +
                   "4. Select 'My Registries' from the dropdown\n" +
                   "5. Search and install 'Microsoft.CodeAnalysis.CSharp' (version 4.14.0 or higher)\n" +
                   "6. See README for detailed instructions.";
        }
    }
}