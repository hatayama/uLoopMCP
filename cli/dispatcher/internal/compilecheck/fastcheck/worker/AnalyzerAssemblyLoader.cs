using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Loads analyzer DLLs into the default context and resolves their dependencies from the
    /// directories the analyzers came from.
    /// </summary>
    internal sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        private readonly object gate = new object();
        private readonly List<string> directories = new List<string>();

        public void AddDependencyLocation(string fullPath)
        {
            RememberDirectoryOf(fullPath);
        }

        public Assembly LoadFromPath(string fullPath)
        {
            RememberDirectoryOf(fullPath);

            // Analyzers each ship their own copy of shared dependencies, and the default context
            // cannot load a second assembly with the same simple name, so the first one wins.
            string simpleName = AssemblyName.GetAssemblyName(fullPath).Name;
            Assembly loaded = FindLoaded(simpleName);
            if (loaded != null)
            {
                return loaded;
            }

            return AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }

        public Assembly Resolve(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            Assembly loaded = FindLoaded(assemblyName.Name);
            if (loaded != null)
            {
                return loaded;
            }

            foreach (string directory in SnapshotDirectories())
            {
                string candidatePath = Path.Combine(directory, assemblyName.Name + ".dll");
                if (File.Exists(candidatePath))
                {
                    return context.LoadFromAssemblyPath(candidatePath);
                }
            }

            return null;
        }

        private static Assembly FindLoaded(string simpleName)
        {
            return AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal));
        }

        private void RememberDirectoryOf(string fullPath)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            lock (gate)
            {
                if (!directories.Contains(directory, StringComparer.Ordinal))
                {
                    directories.Add(directory);
                }
            }
        }

        private List<string> SnapshotDirectories()
        {
            lock (gate)
            {
                return new List<string>(directories);
            }
        }
    }
}
