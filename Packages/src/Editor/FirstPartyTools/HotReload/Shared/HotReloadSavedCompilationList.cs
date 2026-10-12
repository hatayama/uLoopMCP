using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using UnityEngine;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The compilation assembly list as saved for the next domain: only the fields hot reload reads,
    /// plus the file stamps and MVIDs that tell whether each entry still describes the dlls on disk.
    /// </summary>
    internal sealed class HotReloadSavedCompilationList
    {
        public int formatVersion;

        // In the order CompilationPipeline.GetAssemblies returned them.
        public List<HotReloadSavedCompilationAssembly> assemblies;

        // One per distinct compiled assembly reference path, in first-seen order. Assemblies name
        // their references by index, because most of them share the same few hundred paths and
        // repeating the paths made the file several times larger.
        public List<HotReloadSavedReference> references;

        /// <summary>
        /// True when every field a rebuild reads is present and every reference index is in range.
        /// </summary>
        internal bool IsComplete()
        {
            if (assemblies == null || references == null)
            {
                return false;
            }

            foreach (HotReloadSavedReference reference in references)
            {
                if (reference == null || !reference.IsComplete())
                {
                    return false;
                }
            }

            foreach (HotReloadSavedCompilationAssembly assembly in assemblies)
            {
                if (assembly == null || !assembly.IsComplete(references.Count))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads the saved fields of every assembly, the stamp of each assembly's dll and the stamp
        /// and MVID of every referenced dll. Touches only files and managed properties, so it can
        /// run on a pool thread.
        /// </summary>
        internal static HotReloadSavedCompilationList Capture(
            IReadOnlyList<UnityCompilationAssembly> fetched,
            string projectRoot,
            int formatVersion)
        {
            Debug.Assert(fetched != null, "fetched must not be null.");
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");

            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(projectRoot);
            List<HotReloadSavedCompilationAssembly> assemblies = new List<HotReloadSavedCompilationAssembly>(fetched.Count);
            List<HotReloadSavedReference> references = new List<HotReloadSavedReference>();
            Dictionary<string, int> indexByPath = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (UnityCompilationAssembly assembly in fetched)
            {
                string[] referencePaths = assembly.compiledAssemblyReferences ?? Array.Empty<string>();
                int[] referenceIndexes = new int[referencePaths.Length];
                for (int position = 0; position < referencePaths.Length; position++)
                {
                    referenceIndexes[position] = IndexOf(referencePaths[position], projectRoot, indexByPath, references);
                }

                assemblies.Add(HotReloadSavedCompilationAssembly.From(assembly, referenceIndexes, layout));
            }

            return new HotReloadSavedCompilationList
            {
                formatVersion = formatVersion,
                assemblies = assemblies,
                references = references,
            };
        }

        private static int IndexOf(
            string referencePath,
            string projectRoot,
            Dictionary<string, int> indexByPath,
            List<HotReloadSavedReference> references)
        {
            if (indexByPath.TryGetValue(referencePath, out int index))
            {
                return index;
            }

            index = references.Count;
            indexByPath.Add(referencePath, index);
            references.Add(HotReloadSavedReference.Read(referencePath, projectRoot));
            return index;
        }
    }

    /// <summary>
    /// The outcome of loading the saved list: the list, or why there is none to use.
    /// </summary>
    internal readonly struct HotReloadSavedCompilationListLoad
    {
        public readonly HotReloadSavedCompilationList List;
        public readonly string FailureReason;

        private HotReloadSavedCompilationListLoad(HotReloadSavedCompilationList list, string failureReason)
        {
            List = list;
            FailureReason = failureReason;
        }

        internal static HotReloadSavedCompilationListLoad Loaded(HotReloadSavedCompilationList list)
        {
            Debug.Assert(list != null, "list must not be null.");
            return new HotReloadSavedCompilationListLoad(list, null);
        }

        internal static HotReloadSavedCompilationListLoad Failed(string failureReason)
        {
            Debug.Assert(!string.IsNullOrEmpty(failureReason), "failureReason must not be null or empty.");
            return new HotReloadSavedCompilationListLoad(null, failureReason);
        }
    }

    /// <summary>
    /// One saved compilation assembly. Fields hot reload never reads (compiler options, flags, root
    /// namespace) are not saved.
    /// </summary>
    internal sealed class HotReloadSavedCompilationAssembly
    {
        public string name;
        public string outputPath;
        public string[] sourceFiles;
        public string[] defines;

        // Names of the project assemblies this one references, in Unity's order.
        public string[] assemblyReferences;

        // The precompiled assemblies this one references, in Unity's order, as indexes into the
        // list's references.
        public int[] compiledAssemblyReferenceIndexes;

        // Null when the assembly had no dll when the list was saved.
        public HotReloadSavedFileStamp dllStamp;

        internal bool IsComplete(int referenceCount)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(outputPath) || sourceFiles == null
                || defines == null || assemblyReferences == null || compiledAssemblyReferenceIndexes == null)
            {
                return false;
            }

            foreach (int index in compiledAssemblyReferenceIndexes)
            {
                if (index < 0 || index >= referenceCount)
                {
                    return false;
                }
            }

            return true;
        }

        internal static HotReloadSavedCompilationAssembly From(
            UnityCompilationAssembly assembly,
            int[] compiledAssemblyReferenceIndexes,
            CompiledAssemblyLayout layout)
        {
            UnityCompilationAssembly[] referenced = assembly.assemblyReferences ?? Array.Empty<UnityCompilationAssembly>();
            string[] referencedNames = new string[referenced.Length];
            for (int index = 0; index < referenced.Length; index++)
            {
                referencedNames[index] = referenced[index].name;
            }

            return new HotReloadSavedCompilationAssembly
            {
                name = assembly.name,
                outputPath = assembly.outputPath,
                sourceFiles = assembly.sourceFiles ?? Array.Empty<string>(),
                defines = assembly.defines ?? Array.Empty<string>(),
                assemblyReferences = referencedNames,
                compiledAssemblyReferenceIndexes = compiledAssemblyReferenceIndexes,
                dllStamp = HotReloadSavedFileStamp.ReadOrNull(layout.DllPath(assembly.name)),
            };
        }
    }

    /// <summary>
    /// A precompiled assembly reference as it was when the list was saved.
    /// </summary>
    internal sealed class HotReloadSavedReference
    {
        // The path exactly as Unity listed it in compiledAssemblyReferences.
        public string path;

        // Both null when the file did not exist when the list was saved.
        public HotReloadSavedFileStamp stamp;
        public string mvid;

        internal bool IsComplete()
        {
            if (string.IsNullOrEmpty(path) || (stamp == null) != (mvid == null))
            {
                return false;
            }

            return mvid == null || Guid.TryParseExact(mvid, "N", out Guid _);
        }

        internal static HotReloadSavedReference Read(string referencePath, string projectRoot)
        {
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, referencePath));
            HotReloadSavedFileStamp stamp = HotReloadSavedFileStamp.ReadOrNull(fullPath);
            return new HotReloadSavedReference
            {
                path = referencePath,
                stamp = stamp,
                mvid = stamp == null ? null : HotReloadAssemblyMetadataMvid.Read(fullPath).ToString("N"),
            };
        }
    }

    /// <summary>
    /// The length and last write time of a file, which together tell that a dll was not rebuilt.
    /// </summary>
    internal sealed class HotReloadSavedFileStamp
    {
        public long length;
        public long lastWriteTicks;

        /// <summary>
        /// Returns the stamp of the file, or null when the file does not exist.
        /// </summary>
        internal static HotReloadSavedFileStamp ReadOrNull(string fullPath)
        {
            FileInfo file = new FileInfo(fullPath);
            if (!file.Exists)
            {
                return null;
            }

            return new HotReloadSavedFileStamp { length = file.Length, lastWriteTicks = file.LastWriteTimeUtc.Ticks };
        }

        /// <summary>
        /// True when the other stamp has the same length and last write time. False for null, which
        /// stands for a file that does not exist.
        /// </summary>
        internal bool SameAs(HotReloadSavedFileStamp other)
        {
            return other != null && other.length == length && other.lastWriteTicks == lastWriteTicks;
        }
    }

    /// <summary>
    /// Reads a dll's MVID from its metadata header without loading the assembly. Safe on a pool thread.
    /// </summary>
    internal static class HotReloadAssemblyMetadataMvid
    {
        internal static Guid Read(string dllPath)
        {
            using FileStream stream = File.Open(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using PEReader peReader = new PEReader(stream);
            MetadataReader reader = peReader.GetMetadataReader();
            return reader.GetGuid(reader.GetModuleDefinition().Mvid);
        }
    }
}
