#if ULOOPMCP_HAS_ROSLYN
using System;
using System.Collections.Generic;
using System.IO;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Centralized rules for excluding assemblies from Roslyn metadata references.
    /// Keeps analyzer DLLs and other non-runtime assemblies out of the compilation references
    /// to avoid type conflicts with real Unity assemblies.
    /// </summary>
    public static class ReferenceExclusionRules
    {
        private static readonly HashSet<string> ExactFileNamesToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.Unity.Analyzers.dll"
        };

        private static readonly string[] FileNameSubstringsToSkip = new string[]
        {
            "Analyzer",
            "Analyzers"
        };

        private static readonly string[] PathSubstringsToSkip = new string[]
        {
            "/analyzer/",
            "/analyzers/",
            "\\analyzer\\",
            "\\analyzers\\"
        };

        private static readonly HashSet<string> ExactAssemblyNamesToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.Unity.Analyzers"
        };

        private static readonly string[] AssemblyNameSubstringsToSkip = new string[]
        {
            ".Analyzers",
            "Analyzers"
        };

        public static bool ShouldSkip(string dllPath, string assemblyName)
        {
            if (!string.IsNullOrEmpty(dllPath))
            {
                if (MatchesFileNameRules(dllPath)) return true;
                if (MatchesPathRules(dllPath)) return true;
            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                if (MatchesAssemblyNameRules(assemblyName)) return true;
            }

            return false;
        }

        private static bool MatchesFileNameRules(string dllPath)
        {
            string fileName = Path.GetFileName(dllPath);
            if (string.IsNullOrEmpty(fileName)) return false;

            if (ExactFileNamesToSkip.Contains(fileName)) return true;

            string lowerFileName = fileName.ToLowerInvariant();
            for (int i = 0; i < FileNameSubstringsToSkip.Length; i++)
            {
                string token = FileNameSubstringsToSkip[i];
                if (lowerFileName.Contains(token.ToLowerInvariant())) return true;
            }

            return false;
        }

        private static bool MatchesPathRules(string dllPath)
        {
            string lowerPath = dllPath.ToLowerInvariant();
            for (int i = 0; i < PathSubstringsToSkip.Length; i++)
            {
                string token = PathSubstringsToSkip[i];
                if (lowerPath.Contains(token)) return true;
            }

            return false;
        }

        private static bool MatchesAssemblyNameRules(string assemblyName)
        {
            if (ExactAssemblyNamesToSkip.Contains(assemblyName)) return true;

            string lowerAsm = assemblyName.ToLowerInvariant();
            for (int i = 0; i < AssemblyNameSubstringsToSkip.Length; i++)
            {
                string token = AssemblyNameSubstringsToSkip[i];
                if (lowerAsm.Contains(token.ToLowerInvariant())) return true;
            }

            return false;
        }
    }
}
#endif

