using System;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Keeps the last non-empty compilation assembly list so repeated lookups ask Unity only once
    /// until the owner invalidates it. Main thread only, like the Unity call it wraps, except Generation.
    /// </summary>
    internal sealed class HotReloadCompilationAssemblyCache
    {
        private readonly Func<UnityCompilationAssembly[]> _fetch;
        private UnityCompilationAssembly[] _assemblies;
        private int _generation;

        internal HotReloadCompilationAssemblyCache(Func<UnityCompilationAssembly[]> fetch)
        {
            Debug.Assert(fetch != null, "fetch must not be null.");
            _fetch = fetch;
        }

        internal bool IsFilled => _assemblies != null;

        /// <summary>
        /// Counts the invalidations of this domain, so a list built off the main thread can tell
        /// whether the script set changed while it was being built. The only member readable off the
        /// main thread.
        /// </summary>
        internal int Generation => Volatile.Read(ref _generation);

        /// <summary>
        /// Returns the memoized list, asking Unity when nothing is kept. The list is shared; callers only read it.
        /// </summary>
        internal IReadOnlyList<UnityCompilationAssembly> Current()
        {
            if (_assemblies != null)
            {
                return _assemblies;
            }

            UnityCompilationAssembly[] fetched = _fetch();
            Debug.Assert(fetched != null, "fetch must not return null.");

            // Why not memoize an empty result: GetAssemblies() answers nothing while a compile is in
            // flight, and keeping that would hide every assembly for the rest of the domain's life.
            if (fetched.Length > 0)
            {
                _assemblies = fetched;
            }

            return fetched;
        }

        /// <summary>
        /// Returns the first compilation assembly with this name, or null when none has it.
        /// </summary>
        internal UnityCompilationAssembly FindByName(string assemblyName)
        {
            Debug.Assert(!string.IsNullOrEmpty(assemblyName), "assemblyName must not be null or empty.");

            IReadOnlyList<UnityCompilationAssembly> assemblies = Current();
            for (int index = 0; index < assemblies.Count; index++)
            {
                if (assemblies[index].name == assemblyName)
                {
                    return assemblies[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Keeps a list built without asking Unity, when nothing is kept yet and no invalidation
        /// happened since the builder read the generation. Returns whether the list was kept.
        /// </summary>
        internal bool TryFill(UnityCompilationAssembly[] assemblies, int expectedGeneration)
        {
            Debug.Assert(assemblies != null && assemblies.Length > 0, "assemblies must not be null or empty.");

            // Why a kept list wins: a list Unity answered is the truth the rebuilt one only approximates.
            if (_assemblies != null || Generation != expectedGeneration)
            {
                return false;
            }

            _assemblies = assemblies;
            return true;
        }

        internal void Invalidate()
        {
            _assemblies = null;
            Interlocked.Increment(ref _generation);
        }
    }
}
