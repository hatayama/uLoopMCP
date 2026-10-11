using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using UnityEditor.Compilation;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Cross-checks the rebuilt compilation assembly list against Unity's on every assembly of this
    /// project, once from the saved entries and once with every assembly rebuilt from its PDB.
    /// Some packages' PDBs are rewritten after the compile and lose the documents no sequence point
    /// refers to; those must be refused, not rebuilt with fewer sources.
    /// </summary>
    public sealed class HotReloadCompilationAssemblyReconstructionCrossCheckTests
    {
        private string _directory;
        private string _filePath;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "uloop-compilation-list-cross-check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _filePath = Path.Combine(_directory, "CompilationAssemblies.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// What: the list rebuilt from a clean save with every dll unchanged equals
        /// CompilationPipeline.GetAssemblies() in every field hot reload reads, in order.
        /// </summary>
        [Test]
        public void ReconstructedList_MatchesGetAssembliesForEveryField()
        {
            UnityCompilationAssembly[] expected = CompilationPipeline.GetAssemblies();
            HotReloadCompilationAssemblyListStore store = SaveClean(expected);
            HotReloadSnapshotCaptureInputs inputs = HotReloadSnapshotCaptureInputs.ReadOnMainThread();

            HotReloadCompilationListReconstruction reconstruction =
                HotReloadCompilationAssemblyReconstruction.Reconstruct(store, inputs, CancellationToken.None);

            Assert.That(reconstruction.FailureReason, Is.Null, "failure");
            Assert.That(Mismatches(expected, reconstruction.Assemblies), Is.Empty);
        }

        /// <summary>
        /// What: rebuilding every compiled assembly from its PDB gives Unity's fields, in order, for
        /// every assembly whose PDB lists as many sources as the compiler was given, and
        /// pdb_documents_incomplete for every assembly whose PDB lost some of Unity's sources.
        /// </summary>
        [Test]
        public void EveryAssemblyRebuiltFromItsPdb_MatchesGetAssemblies_OrIsRefusedWhenThePdbLostSources()
        {
            UnityCompilationAssembly[] expected = CompilationPipeline.GetAssemblies();
            HotReloadSavedCompilationList saved = SaveClean(expected).Load().List;
            HotReloadSnapshotCaptureInputs inputs = HotReloadSnapshotCaptureInputs.ReadOnMainThread();
            HotReloadCompilationAssemblyRebuilder rebuilder = new HotReloadCompilationAssemblyRebuilder(saved, inputs);
            Dictionary<string, UnityCompilationAssembly> expectedByName = new Dictionary<string, UnityCompilationAssembly>();
            foreach (UnityCompilationAssembly assembly in expected)
            {
                expectedByName[assembly.name] = assembly;
            }

            List<string> mismatches = new List<string>();
            List<string> refused = new List<string>();
            List<string> lostSources = new List<string>();
            int rebuilt = 0;
            foreach (HotReloadCaptureAssemblyCandidate candidate in HotReloadCaptureAssemblyEnumerator.ListCompiled(inputs.ProjectRoot))
            {
                if (!expectedByName.TryGetValue(candidate.Name, out UnityCompilationAssembly want))
                {
                    continue;
                }

                HotReloadSavedCompilationAssembly savedAssembly = saved.assemblies.Find(assembly => assembly.name == candidate.Name);
                string reason = rebuilder.TryFromPdb(savedAssembly, candidate, out HotReloadRebuiltAssemblyEntry entry);
                if (LostSources(want, candidate, inputs))
                {
                    lostSources.Add(candidate.Name);
                }

                if (reason == HotReloadConstants.CompilationListReasonPdbDocumentsIncomplete)
                {
                    refused.Add(candidate.Name);
                    continue;
                }

                Assert.That(reason, Is.Null, candidate.Name + " failure");
                rebuilt++;
                AddEntryMismatches(mismatches, want, entry);
            }

            Debug.Log("[cross-check] rebuilt " + rebuilt + ", refused " + refused.Count + ", lost sources " + lostSources.Count);
            Assert.That(rebuilt, Is.GreaterThan(0), "Precondition: some assembly must be rebuilt.");
            Assert.That(mismatches, Is.Empty, "rebuilt entries");
            // Why only a superset: a PDB may also be refused for another shortfall against the
            // compiler's count, which the sources Unity lists need not show.
            Assert.That(refused, Is.SupersetOf(lostSources), "refused assemblies");
        }

        private HotReloadCompilationAssemblyListStore SaveClean(UnityCompilationAssembly[] fetched)
        {
            HotReloadCompilationAssemblyListStore store = CreateStore();
            store.SaveInBackgroundIfClean(fetched, 0, Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            Assert.That(File.Exists(_filePath), Is.True, "Precondition: the list must be saved.");
            return store;
        }

        // True when some source Unity lists for the assembly is not among the PDB's documents.
        private static bool LostSources(
            UnityCompilationAssembly want,
            HotReloadCaptureAssemblyCandidate candidate,
            HotReloadSnapshotCaptureInputs inputs)
        {
            HashSet<string> inPdb = new HashSet<string>(HotReloadCaptureAssemblyEnumerator.ReadSources(candidate, inputs).SourceAssetPaths);
            return !inPdb.IsSupersetOf(want.sourceFiles);
        }

        private static void AddEntryMismatches(List<string> mismatches, UnityCompilationAssembly want, HotReloadRebuiltAssemblyEntry got)
        {
            AddIfDifferent(mismatches, want.name, "outputPath", new[] { want.outputPath }, new[] { got.OutputPath });
            AddIfDifferent(mismatches, want.name, "sourceFiles", want.sourceFiles, got.SourceFiles);
            AddIfDifferent(mismatches, want.name, "defines", want.defines, got.Defines);
            AddIfDifferent(mismatches, want.name, "assemblyReferences", NamesOf(want.assemblyReferences), got.ProjectReferences.ToArray());
            AddIfDifferent(mismatches, want.name, "compiledAssemblyReferences", want.compiledAssemblyReferences, got.CompiledReferences.ToArray());
        }

        private HotReloadCompilationAssemblyListStore CreateStore()
        {
            // Why inline: EditMode tests must not start pool work, so the "background" runs here.
            return new HotReloadCompilationAssemblyListStore(
                _filePath,
                () => false,
                () => 0,
                work =>
                {
                    work();
                    return Task.CompletedTask;
                });
        }

        private static List<string> Mismatches(UnityCompilationAssembly[] expected, UnityCompilationAssembly[] actual)
        {
            List<string> mismatches = new List<string>();
            if (actual == null || actual.Length != expected.Length)
            {
                mismatches.Add("count " + expected.Length + " != " + (actual == null ? "null" : actual.Length.ToString()));
                return mismatches;
            }

            for (int index = 0; index < expected.Length; index++)
            {
                UnityCompilationAssembly want = expected[index];
                UnityCompilationAssembly got = actual[index];
                AddIfDifferent(mismatches, want.name, "name", new[] { want.name }, new[] { got.name });
                AddIfDifferent(mismatches, want.name, "outputPath", new[] { want.outputPath }, new[] { got.outputPath });
                AddIfDifferent(mismatches, want.name, "sourceFiles", want.sourceFiles, got.sourceFiles);
                AddIfDifferent(mismatches, want.name, "defines", want.defines, got.defines);
                AddIfDifferent(mismatches, want.name, "assemblyReferences", NamesOf(want.assemblyReferences), NamesOf(got.assemblyReferences));
                AddIfDifferent(mismatches, want.name, "compiledAssemblyReferences", want.compiledAssemblyReferences, got.compiledAssemblyReferences);
                AddIfDifferent(mismatches, want.name, "allReferences", want.allReferences, got.allReferences);
            }

            return mismatches;
        }

        private static string[] NamesOf(UnityCompilationAssembly[] assemblies)
        {
            string[] names = new string[assemblies.Length];
            for (int index = 0; index < assemblies.Length; index++)
            {
                names[index] = assemblies[index].name;
            }

            return names;
        }

        private static void AddIfDifferent(List<string> mismatches, string assemblyName, string field, string[] want, string[] got)
        {
            if (want.Length == got.Length && Array.TrueForAll(Indexes(want.Length), index => want[index] == got[index]))
            {
                return;
            }

            bool sameSet = new HashSet<string>(want).SetEquals(got);
            mismatches.Add(assemblyName + "." + field + (sameSet ? " (order only)" : " (content)"));
        }

        private static int[] Indexes(int count)
        {
            int[] indexes = new int[count];
            for (int index = 0; index < count; index++)
            {
                indexes[index] = index;
            }

            return indexes;
        }
    }
}
