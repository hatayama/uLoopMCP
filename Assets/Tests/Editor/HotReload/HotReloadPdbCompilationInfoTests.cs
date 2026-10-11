using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Contract of reading the defines and metadata references a portable PDB records, and a
    /// cross-check of both against what Unity reports for every compiled assembly of this project.
    /// Whether they agree decides if the compilation list can be rebuilt from PDBs at startup.
    /// </summary>
    public sealed class HotReloadPdbCompilationInfoTests
    {
        private const string TestAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const string ReferenceAssemblySuffix = ".ref.dll";
        private const string AssemblySuffix = ".dll";
        private const int MaxListedDifferences = 10;

        /// <summary>
        /// What: the test assembly's own PDB yields the editor and test defines it was compiled with,
        /// and references whose file names and MVIDs are filled in.
        /// </summary>
        [Test]
        public void Read_ReturnsTheDefinesAndReferencesOfTheTestAssembly()
        {
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(ProjectRoot());

            HotReloadPdbCompilationInfo info = HotReloadPdbCompilationInfo.Read(layout.PdbPath(TestAssemblyName));

            CollectionAssert.Contains(info.Defines, "UNITY_EDITOR");
            CollectionAssert.Contains(info.Defines, "UNITY_INCLUDE_TESTS");
            Assert.That(info.References, Is.Not.Empty);
            Assert.That(info.References.Select(reference => reference.FileName), Has.Some.EqualTo("nunit.framework.dll"));
            Assert.That(info.References.Select(reference => reference.FileName), Has.All.EndsWith(AssemblySuffix));
            Assert.That(info.References.Select(reference => reference.Mvid), Has.None.EqualTo(Guid.Empty));
        }

        /// <summary>
        /// What: for every compiled assembly with a PDB, the PDB's defines equal Unity's defines in
        /// order, and its reference file names equal the file names of Unity's references, counting a
        /// project assembly's X.ref.dll as X.dll. Each precompiled reference also carries the MVID of
        /// the dll Unity lists for it, which is what the PDB's MVID is later compared with.
        /// </summary>
        [Test]
        public void PdbCompilationInfo_MatchesGetAssembliesForEveryCompiledAssembly()
        {
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(ProjectRoot());
            List<string> mismatches = new List<string>();
            Dictionary<string, string> mvidByReferencePath = new Dictionary<string, string>(StringComparer.Ordinal);
            int checkedAssemblies = 0;

            foreach (UnityCompilationAssembly assembly in HotReloadCompilationAssemblies.Current())
            {
                string pdbPath = layout.PdbPath(assembly.name);
                if (!File.Exists(layout.DllPath(assembly.name)) || !File.Exists(pdbPath))
                {
                    continue;
                }

                HotReloadPdbCompilationInfo info = HotReloadPdbCompilationInfo.Read(pdbPath);
                checkedAssemblies++;
                AddDefineMismatch(assembly, info, mismatches);
                AddReferenceMismatch(assembly, info, mismatches);
                AddMvidMismatches(assembly, info, mvidByReferencePath, mismatches);
            }

            Assert.That(checkedAssemblies, Is.GreaterThan(0), "No compiled assembly with a PDB was found.");
            Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
        }

        private static void AddDefineMismatch(
            UnityCompilationAssembly assembly,
            HotReloadPdbCompilationInfo info,
            List<string> mismatches)
        {
            string[] fromUnity = assembly.defines ?? Array.Empty<string>();
            if (info.Defines.SequenceEqual(fromUnity, StringComparer.Ordinal))
            {
                return;
            }

            // The order question is reported separately from a content difference.
            bool sameSet = new HashSet<string>(info.Defines, StringComparer.Ordinal).SetEquals(fromUnity);
            mismatches.Add(
                assembly.name + ": defines differ " + (sameSet ? "in order only" : "in content")
                + DescribeDifference(info.Defines, fromUnity));
        }

        private static void AddReferenceMismatch(
            UnityCompilationAssembly assembly,
            HotReloadPdbCompilationInfo info,
            List<string> mismatches)
        {
            HashSet<string> fromPdb = new HashSet<string>(
                info.References.Select(reference => ToImplementationFileName(reference.FileName)),
                StringComparer.Ordinal);
            HashSet<string> fromUnity = new HashSet<string>(
                (assembly.allReferences ?? Array.Empty<string>()).Select(Path.GetFileName),
                StringComparer.Ordinal);
            if (fromPdb.SetEquals(fromUnity))
            {
                return;
            }

            mismatches.Add(assembly.name + ": reference file names differ" + DescribeDifference(fromPdb, fromUnity));
        }

        private static void AddMvidMismatches(
            UnityCompilationAssembly assembly,
            HotReloadPdbCompilationInfo info,
            Dictionary<string, string> mvidByReferencePath,
            List<string> mismatches)
        {
            Dictionary<string, string> pathByFileName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string referencePath in assembly.compiledAssemblyReferences ?? Array.Empty<string>())
            {
                pathByFileName[Path.GetFileName(referencePath)] = referencePath;
            }

            List<string> wrong = new List<string>();
            foreach (HotReloadPdbReferencedAssembly reference in info.References)
            {
                if (!pathByFileName.TryGetValue(reference.FileName, out string referencePath))
                {
                    continue;
                }

                if (!mvidByReferencePath.TryGetValue(referencePath, out string expected))
                {
                    expected = HotReloadAssemblyMvid.Read(Path.GetFullPath(Path.Combine(ProjectRoot(), referencePath)));
                    mvidByReferencePath[referencePath] = expected;
                }

                if (reference.Mvid.ToString("N") != expected)
                {
                    wrong.Add(reference.FileName);
                }
            }

            if (wrong.Count == 0)
            {
                return;
            }

            mismatches.Add(
                assembly.name + ": " + wrong.Count + " reference MVIDs differ from the dll ("
                + string.Join(", ", wrong.Take(MaxListedDifferences)) + ")");
        }

        private static string DescribeDifference(IEnumerable<string> fromPdb, IEnumerable<string> fromUnity)
        {
            List<string> onlyInPdb = fromPdb.Except(fromUnity, StringComparer.Ordinal).ToList();
            List<string> onlyInUnity = fromUnity.Except(fromPdb, StringComparer.Ordinal).ToList();
            StringBuilder message = new StringBuilder();
            message.Append(" (only in PDB: ").Append(onlyInPdb.Count).Append(' ');
            message.Append(string.Join(", ", onlyInPdb.Take(MaxListedDifferences)));
            message.Append("; only in Unity: ").Append(onlyInUnity.Count).Append(' ');
            message.Append(string.Join(", ", onlyInUnity.Take(MaxListedDifferences)));
            message.Append(')');
            return message.ToString();
        }

        private static string ToImplementationFileName(string fileName)
        {
            if (!fileName.EndsWith(ReferenceAssemblySuffix, StringComparison.Ordinal))
            {
                return fileName;
            }

            return fileName.Substring(0, fileName.Length - ReferenceAssemblySuffix.Length) + AssemblySuffix;
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
