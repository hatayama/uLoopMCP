using System;
using System.Collections.Generic;
using System.Threading;

using UnityEditor.Compilation;

using UnityEngine;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The outcome of rebuilding the compilation assembly list: the whole list, or why there is none.
    /// </summary>
    internal readonly struct HotReloadCompilationListReconstruction
    {
        public readonly UnityCompilationAssembly[] Assemblies;
        public readonly string FailureReason;

        private HotReloadCompilationListReconstruction(UnityCompilationAssembly[] assemblies, string failureReason)
        {
            Assemblies = assemblies;
            FailureReason = failureReason;
        }

        internal static HotReloadCompilationListReconstruction Rebuilt(UnityCompilationAssembly[] assemblies)
        {
            Debug.Assert(assemblies != null && assemblies.Length > 0, "assemblies must not be null or empty.");
            return new HotReloadCompilationListReconstruction(assemblies, null);
        }

        internal static HotReloadCompilationListReconstruction Failed(string failureReason)
        {
            Debug.Assert(!string.IsNullOrEmpty(failureReason), "failureReason must not be null or empty.");
            return new HotReloadCompilationListReconstruction(null, failureReason);
        }
    }

    /// <summary>
    /// Rebuilds the compilation assembly list of this domain from the list a previous clean domain
    /// saved and from the compiled dlls and PDBs, without asking Unity, so it can run on a pool
    /// thread. An assembly whose dll did not change keeps its saved entry; one that was compiled
    /// again takes its sources, defines and references from its PDB. Returns no list at all when
    /// any part cannot be shown to match the files on disk, because a partial list would mislead
    /// the default file choice and the patch target lookup.
    /// </summary>
    internal static class HotReloadCompilationAssemblyReconstruction
    {
        internal static HotReloadCompilationListReconstruction Reconstruct(
            HotReloadCompilationAssemblyListStore store,
            HotReloadSnapshotCaptureInputs inputs,
            CancellationToken ct)
        {
            Debug.Assert(store != null, "store must not be null.");
            Debug.Assert(inputs != null, "inputs must not be null.");

            HotReloadSavedCompilationListLoad load = store.Load();
            if (load.List == null)
            {
                return HotReloadCompilationListReconstruction.Failed(load.FailureReason);
            }

            HotReloadSavedCompilationList saved = load.List;
            Dictionary<string, HotReloadCaptureAssemblyCandidate> compiled = CompiledByName(inputs.ProjectRoot);
            if (!HasTheSavedAssemblySet(saved, compiled))
            {
                return HotReloadCompilationListReconstruction.Failed(HotReloadConstants.CompilationListReasonAssemblySetChanged);
            }

            HotReloadCompilationAssemblyRebuilder rebuilder = new HotReloadCompilationAssemblyRebuilder(saved, inputs);
            List<HotReloadRebuiltAssemblyEntry> entries = new List<HotReloadRebuiltAssemblyEntry>(saved.assemblies.Count);
            foreach (HotReloadSavedCompilationAssembly assembly in saved.assemblies)
            {
                ct.ThrowIfCancellationRequested();

                // Why an unchanged dll keeps its entry: the compile is deterministic, so the same dll
                // came from the same sources, defines and references.
                if (!compiled.TryGetValue(assembly.name, out HotReloadCaptureAssemblyCandidate candidate)
                    || assembly.dllStamp.SameAs(HotReloadSavedFileStamp.ReadOrNull(candidate.DllPath)))
                {
                    entries.Add(rebuilder.FromSaved(assembly));
                    continue;
                }

                string failure = rebuilder.TryFromPdb(assembly, candidate, out HotReloadRebuiltAssemblyEntry rebuilt);
                if (failure != null)
                {
                    return HotReloadCompilationListReconstruction.Failed(failure);
                }

                entries.Add(rebuilt);
            }

            return Build(entries);
        }

        private static Dictionary<string, HotReloadCaptureAssemblyCandidate> CompiledByName(string projectRoot)
        {
            Dictionary<string, HotReloadCaptureAssemblyCandidate> compiled =
                new Dictionary<string, HotReloadCaptureAssemblyCandidate>(StringComparer.Ordinal);
            foreach (HotReloadCaptureAssemblyCandidate candidate in HotReloadCaptureAssemblyEnumerator.ListCompiled(projectRoot))
            {
                compiled[candidate.Name] = candidate;
            }

            return compiled;
        }

        // Why the set is compared before any entry is used: an added or removed asmdef changes the
        // list in ways no single dll shows.
        private static bool HasTheSavedAssemblySet(
            HotReloadSavedCompilationList saved,
            Dictionary<string, HotReloadCaptureAssemblyCandidate> compiled)
        {
            int savedWithDll = 0;
            foreach (HotReloadSavedCompilationAssembly assembly in saved.assemblies)
            {
                if (assembly.dllStamp == null)
                {
                    continue;
                }

                if (!compiled.ContainsKey(assembly.name))
                {
                    return false;
                }

                savedWithDll++;
            }

            return savedWithDll == compiled.Count;
        }

        private static HotReloadCompilationListReconstruction Build(List<HotReloadRebuiltAssemblyEntry> entries)
        {
            Dictionary<string, HotReloadRebuiltAssemblyEntry> entryByName =
                new Dictionary<string, HotReloadRebuiltAssemblyEntry>(StringComparer.Ordinal);
            foreach (HotReloadRebuiltAssemblyEntry entry in entries)
            {
                entryByName[entry.Name] = entry;
            }

            foreach (HotReloadRebuiltAssemblyEntry entry in entries)
            {
                foreach (string referencedName in entry.ProjectReferences)
                {
                    if (!entryByName.ContainsKey(referencedName))
                    {
                        return HotReloadCompilationListReconstruction.Failed(
                            HotReloadConstants.CompilationListReasonUnresolvedReference);
                    }
                }
            }

            Dictionary<string, UnityCompilationAssembly> built =
                new Dictionary<string, UnityCompilationAssembly>(StringComparer.Ordinal);
            UnityCompilationAssembly[] assemblies = new UnityCompilationAssembly[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                assemblies[index] = BuildAssembly(entries[index], entryByName, built, new HashSet<string>(StringComparer.Ordinal));
            }

            return HotReloadCompilationListReconstruction.Rebuilt(assemblies);
        }

        // Why the referenced assemblies are made first: Unity takes assemblyReferences only through
        // the constructor.
        private static UnityCompilationAssembly BuildAssembly(
            HotReloadRebuiltAssemblyEntry entry,
            Dictionary<string, HotReloadRebuiltAssemblyEntry> entryByName,
            Dictionary<string, UnityCompilationAssembly> built,
            HashSet<string> inProgress)
        {
            if (built.TryGetValue(entry.Name, out UnityCompilationAssembly existing))
            {
                return existing;
            }

            if (!inProgress.Add(entry.Name))
            {
                throw new InvalidOperationException("The saved compilation assemblies reference each other in a cycle at " + entry.Name + ".");
            }

            UnityCompilationAssembly[] references = new UnityCompilationAssembly[entry.ProjectReferences.Count];
            for (int index = 0; index < references.Length; index++)
            {
                references[index] = BuildAssembly(entryByName[entry.ProjectReferences[index]], entryByName, built, inProgress);
            }

            // Hot reload never reads compiler options, flags or the root namespace, and Unity's
            // language version cannot be set from outside, so they keep their defaults.
            UnityCompilationAssembly assembly = new UnityCompilationAssembly(
                entry.Name,
                entry.OutputPath,
                entry.SourceFiles,
                entry.Defines,
                references,
                entry.CompiledReferences.ToArray(),
                AssemblyFlags.None,
                new ScriptCompilerOptions(),
                string.Empty);
            built.Add(entry.Name, assembly);
            return assembly;
        }
    }
}
