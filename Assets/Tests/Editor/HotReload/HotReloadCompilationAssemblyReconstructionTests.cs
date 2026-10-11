using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using NUnit.Framework;

using UnityEditor.Compilation;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Contract of rebuilding the compilation assembly list from the saved list and the compiled
    /// files: a saved entry is used as it is only while its dll is unchanged, a compiled-again
    /// assembly is rebuilt from its PDB, and anything that cannot be shown to match the files on
    /// disk gives no list at all. Each case saves this project's real list, then changes the one
    /// part of the saved file the case is about; the dlls on disk are never touched.
    /// </summary>
    public sealed class HotReloadCompilationAssemblyReconstructionTests
    {
        private const string TargetAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const string TargetReferenceFileName = "nunit.framework.dll";
        private const string SavedOnlyDefine = "SAVED_ONLY_DEFINE";
        private const string RemovedAssemblyName = "Removed.Assembly";

        private string _directory;
        private string _filePath;
        private string _projectRoot;
        private int _backgroundRuns;
        private bool _isCompilationFailed;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "uloop-compilation-list-rebuild-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _filePath = Path.Combine(_directory, "CompilationAssemblies.json");
            _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _backgroundRuns = 0;
            _isCompilationFailed = false;
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
        /// What: without a saved list the rebuild gives no list and passes on why the load failed.
        /// </summary>
        [Test]
        public void Reconstruct_WithoutAUsableSavedList_ReturnsNull()
        {
            HotReloadCompilationListReconstruction result = Reconstruct(CreateStore());

            Assert.That(result.Assemblies, Is.Null, "list");
            Assert.That(result.FailureReason, Is.EqualTo(HotReloadConstants.CompilationListReasonNoSavedList));
        }

        /// <summary>
        /// What: a saved list that names a dll no longer compiled, or lacks one that is, gives no list.
        /// </summary>
        [TestCase(true, TestName = "Reconstruct_WhenTheAssemblySetChanged_ReturnsNull(added)")]
        [TestCase(false, TestName = "Reconstruct_WhenTheAssemblySetChanged_ReturnsNull(removed)")]
        public void Reconstruct_WhenTheAssemblySetChanged_ReturnsNull(bool savedHasAnExtraDll)
        {
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved =>
            {
                if (savedHasAnExtraDll)
                {
                    HotReloadSavedCompilationAssembly extra = JsonConvert.DeserializeObject<HotReloadSavedCompilationAssembly>(
                        JsonConvert.SerializeObject(Target(saved)));
                    extra.name = RemovedAssemblyName;
                    saved.assemblies.Add(extra);
                    return;
                }

                Target(saved).dllStamp = null;
            });

            HotReloadCompilationListReconstruction result = Reconstruct(store);

            Assert.That(result.Assemblies, Is.Null, "list");
            Assert.That(result.FailureReason, Is.EqualTo(HotReloadConstants.CompilationListReasonAssemblySetChanged));
        }

        /// <summary>
        /// What: when no dll changed, every entry is the saved one, even where the PDB says otherwise.
        /// </summary>
        [Test]
        public void Reconstruct_AllStampsUnchanged_RebuildsTheSavedList()
        {
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved => Target(saved).defines = new[] { SavedOnlyDefine });

            HotReloadCompilationListReconstruction result = Reconstruct(store);

            Assert.That(result.FailureReason, Is.Null, "failure");
            Assert.That(Find(result.Assemblies, TargetAssemblyName).defines, Is.EqualTo(new[] { SavedOnlyDefine }));
        }

        /// <summary>
        /// What: an assembly whose dll changed takes its sources, defines, project references and
        /// precompiled references from its PDB, dropping what the saved entry had but the PDB no
        /// longer records, so it equals Unity's entry field by field and in order.
        /// </summary>
        [Test]
        public void Reconstruct_RecompiledAssembly_TakesSourcesDefinesAndReferencesFromItsPdb()
        {
            UnityCompilationAssembly expected = CompilationPipeline.GetAssemblies()[IndexOf(TargetAssemblyName)];
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved =>
            {
                HotReloadSavedCompilationAssembly target = MarkRecompiled(saved);
                target.defines = new[] { SavedOnlyDefine };
                target.sourceFiles = Append(target.sourceFiles, "Assets/NotOnDisk/Removed.cs");
                target.assemblyReferences = Append(target.assemblyReferences, RemovedAssemblyName);
                target.compiledAssemblyReferenceIndexes = AppendIndex(
                    target.compiledAssemblyReferenceIndexes, IndexOfReferenceNotIn(saved, target));
            });

            HotReloadCompilationListReconstruction result = Reconstruct(store);

            Assert.That(result.FailureReason, Is.Null, "failure");
            UnityCompilationAssembly rebuilt = Find(result.Assemblies, TargetAssemblyName);
            Assert.That(rebuilt.sourceFiles, Is.EqualTo(expected.sourceFiles), "sourceFiles");
            Assert.That(rebuilt.defines, Is.EqualTo(expected.defines), "defines");
            Assert.That(NamesOf(rebuilt.assemblyReferences), Is.EqualTo(NamesOf(expected.assemblyReferences)), "assemblyReferences");
            Assert.That(rebuilt.compiledAssemblyReferences, Is.EqualTo(expected.compiledAssemblyReferences), "compiledAssemblyReferences");
        }

        /// <summary>
        /// What: a reference the PDB records under a file name no saved path has gives no list.
        /// </summary>
        [Test]
        public void Reconstruct_UnresolvedReference_ReturnsNull()
        {
            AssertRecompiledTargetGives(
                HotReloadConstants.CompilationListReasonUnresolvedReference,
                reference => reference.path = reference.path.Replace(TargetReferenceFileName, "renamed.dll"));
        }

        /// <summary>
        /// What: a reference whose saved path no longer exists gives no list.
        /// </summary>
        [Test]
        public void Reconstruct_MissingReferenceFile_ReturnsNull()
        {
            AssertRecompiledTargetGives(
                HotReloadConstants.CompilationListReasonReferenceMissing,
                reference =>
                {
                    reference.path = "Library/NotOnDisk/" + TargetReferenceFileName;
                    reference.stamp = null;
                    reference.mvid = null;
                });
        }

        /// <summary>
        /// What: a reference whose file holds another MVID than the PDB records gives no list. The
        /// saved MVID stands for the file because its stamp is unchanged.
        /// </summary>
        [Test]
        public void Reconstruct_ReferenceWithAnotherMvid_ReturnsNull()
        {
            AssertRecompiledTargetGives(
                HotReloadConstants.CompilationListReasonReferenceChanged,
                reference => reference.mvid = Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// What: a recompiled assembly whose PDB was rewritten after the compile and lost the
        /// documents no sequence point refers to (here by a Cecil round trip, as an IL
        /// post-processor does) is refused, instead of being rebuilt with fewer sources.
        /// </summary>
        [Test]
        public void TryFromPdb_PdbThatLostSourcesInARewrite_IsRefused()
        {
            HotReloadSavedCompilationList saved = SaveCleanThenEdit(edit => { }).Load().List;
            string scriptAssemblies = Path.Combine(_directory, "Project", HotReloadConstants.ScriptAssembliesRelativeDirectory);
            Directory.CreateDirectory(scriptAssemblies);
            string dllPath = Path.Combine(scriptAssemblies, TargetAssemblyName + ".dll");
            string pdbPath = Path.ChangeExtension(dllPath, ".pdb");
            CecilPdbRewrite.Rewrite(_projectRoot, TargetAssemblyName, _directory, dllPath);
            HotReloadPdbCompilationInfo rewritten = HotReloadPdbCompilationInfo.Read(pdbPath);
            Assert.That(rewritten.SourceFileCount, Is.Not.Null, "Precondition: the rewritten PDB must keep the compiler's source count.");
            Assert.That(rewritten.DocumentCount, Is.LessThan(rewritten.SourceFileCount.Value), "Precondition: the rewrite must drop some document.");
            HotReloadCompilationAssemblyRebuilder rebuilder =
                new HotReloadCompilationAssemblyRebuilder(saved, HotReloadSnapshotCaptureInputs.ReadOnMainThread());

            string reason = rebuilder.TryFromPdb(
                Target(saved),
                new HotReloadCaptureAssemblyCandidate(TargetAssemblyName, dllPath, pdbPath),
                out HotReloadRebuiltAssemblyEntry entry);

            Assert.That(reason, Is.EqualTo(HotReloadConstants.CompilationListReasonPdbDocumentsIncomplete));
            Assert.That(entry, Is.Null, "entry");
        }

        /// <summary>
        /// What: a recompiled assembly whose PDB belongs to another build of the dll gives no list.
        /// </summary>
        [Test]
        public void Reconstruct_PdbOfAnotherBuild_ReturnsNull()
        {
            string scriptAssemblies = Path.Combine(_directory, "Project", HotReloadConstants.ScriptAssembliesRelativeDirectory);
            Directory.CreateDirectory(scriptAssemblies);
            CompiledAssemblyLayout real = CompiledAssemblyLayout.Resolve(_projectRoot);
            File.Copy(real.DllPath(TargetAssemblyName), Path.Combine(scriptAssemblies, TargetAssemblyName + ".dll"));
            File.Copy(real.PdbPath("UnityCLILoop.ToolContracts"), Path.Combine(scriptAssemblies, TargetAssemblyName + ".pdb"));
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(new HotReloadSavedCompilationList
            {
                formatVersion = HotReloadCompilationAssemblyListStore.FormatVersion,
                assemblies = new List<HotReloadSavedCompilationAssembly>
                {
                    new HotReloadSavedCompilationAssembly
                    {
                        name = TargetAssemblyName,
                        outputPath = "Library/ScriptAssemblies/" + TargetAssemblyName + ".dll",
                        sourceFiles = Array.Empty<string>(),
                        defines = Array.Empty<string>(),
                        assemblyReferences = Array.Empty<string>(),
                        compiledAssemblyReferenceIndexes = Array.Empty<int>(),
                        dllStamp = new HotReloadSavedFileStamp { length = 1, lastWriteTicks = 1 },
                    },
                },
                references = new List<HotReloadSavedReference>(),
            }));
            HotReloadSnapshotCaptureInputs inputs = new HotReloadSnapshotCaptureInputs(
                Path.Combine(_directory, "Project"),
                default(HotReloadCompileStart),
                Array.Empty<HotReloadSnapshotPackageRoot>(),
                StringComparison.Ordinal);

            HotReloadCompilationListReconstruction result =
                HotReloadCompilationAssemblyReconstruction.Reconstruct(CreateStore(), inputs, CancellationToken.None);

            Assert.That(result.Assemblies, Is.Null, "list");
            Assert.That(result.FailureReason, Is.EqualTo(HotReloadConstants.CompilationListReasonPdbMismatch));
        }

        /// <summary>
        /// What: a rebuilt list that fills the empty cache is saved again as the next domain's
        /// baseline, and Unity is not asked.
        /// </summary>
        [Test]
        public void ReconstructAndFill_WhenFilled_SavesTheRebuiltListAsTheNewBaseline()
        {
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved => { });
            int savesBefore = _backgroundRuns;
            int fetches = 0;
            HotReloadCompilationAssemblyCache cache = new HotReloadCompilationAssemblyCache(() =>
            {
                fetches++;
                return Array.Empty<UnityCompilationAssembly>();
            });

            Task<bool> refill = CreateRefill(store, cache).RunAsync();

            Assert.That(refill.Status, Is.EqualTo(TaskStatus.RanToCompletion), "refill status");
            Assert.That(refill.Result, Is.True, "kept");
            Assert.That(cache.IsFilled, Is.True, "cache filled");
            Assert.That(_backgroundRuns, Is.EqualTo(savesBefore + 1), "saves");
            Assert.That(store.Load().List, Is.Not.Null, "saved list readable");
            cache.Current();
            Assert.That(fetches, Is.EqualTo(0), "Unity asked");
        }

        /// <summary>
        /// What: when a caller already filled the cache from Unity, the rebuilt list is neither kept
        /// nor saved.
        /// </summary>
        [Test]
        public void ReconstructAndFill_WhenTheCacheIsAlreadyFilled_KeepsAndSavesNothing()
        {
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved => { });
            int savesBefore = _backgroundRuns;
            UnityCompilationAssembly[] fetched = CompilationPipeline.GetAssemblies();
            HotReloadCompilationAssemblyCache cache = new HotReloadCompilationAssemblyCache(() => fetched);
            cache.Current();

            Task<bool> refill = CreateRefill(store, cache).RunAsync();

            Assert.That(refill.Status, Is.EqualTo(TaskStatus.RanToCompletion), "refill status");
            Assert.That(refill.Result, Is.False, "kept");
            Assert.That(cache.Current(), Is.SameAs(fetched), "cache content");
            Assert.That(_backgroundRuns, Is.EqualTo(savesBefore), "saves");
        }

        /// <summary>
        /// What: a rebuild cancelled while it runs in the background keeps nothing, saves nothing and
        /// reports the cancellation instead of faulting.
        /// </summary>
        [Test]
        public async Task ReconstructAndFill_CancelledWhileRebuilding_KeepsNothing()
        {
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved => { });
            int savesBefore = _backgroundRuns;
            HotReloadCompilationAssemblyCache cache = new HotReloadCompilationAssemblyCache(Array.Empty<UnityCompilationAssembly>);
            TaskCompletionSource<HotReloadCompilationListReconstruction> held =
                new TaskCompletionSource<HotReloadCompilationListReconstruction>();
            Func<HotReloadCompilationListReconstruction> rebuild = null;
            HotReloadCompilationListRefill refill = new HotReloadCompilationListRefill(
                store,
                cache,
                HotReloadSnapshotCaptureInputs.ReadOnMainThread,
                () => _isCompilationFailed,
                work =>
                {
                    rebuild = work;
                    return held.Task;
                });
            VibeLogger.ClearMemoryLogs();

            Task<bool> run = refill.RunAsync();
            HotReloadCompilationListReconstruction rebuilt = rebuild();
            Assert.That(rebuilt.Assemblies, Is.Not.Null, "Precondition: the rebuild must give a list.");
            refill.Cancel();
            held.SetResult(rebuilt);
            // Why awaited: the rest of the run resumes off this thread after the held rebuild completes.
            bool kept = await run;

            Assert.That(run.Status, Is.EqualTo(TaskStatus.RanToCompletion), "refill status");
            Assert.That(kept, Is.False, "kept");
            Assert.That(cache.IsFilled, Is.False, "cache filled");
            Assert.That(_backgroundRuns, Is.EqualTo(savesBefore), "saves");
            Assert.That(
                (string)HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogCompilationListReconstructed)["reason"],
                Is.EqualTo(HotReloadConstants.CompilationListReasonCancelled));
        }

        /// <summary>
        /// What: after a failed compile the rebuilt list is neither kept nor saved, because the dlls
        /// are older than the sources Unity now lists.
        /// </summary>
        [Test]
        public void ReconstructAndFill_WhileCompilationFailed_KeepsAndSavesNothing()
        {
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved => { });
            int savesBefore = _backgroundRuns;
            HotReloadCompilationAssemblyCache cache = new HotReloadCompilationAssemblyCache(Array.Empty<UnityCompilationAssembly>);
            _isCompilationFailed = true;
            VibeLogger.ClearMemoryLogs();

            Task<bool> refill = CreateRefill(store, cache).RunAsync();

            Assert.That(refill.Status, Is.EqualTo(TaskStatus.RanToCompletion), "refill status");
            Assert.That(refill.Result, Is.False, "kept");
            Assert.That(cache.IsFilled, Is.False, "cache filled");
            Assert.That(_backgroundRuns, Is.EqualTo(savesBefore), "saves");
            Assert.That(
                (string)HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogCompilationListReconstructed)["reason"],
                Is.EqualTo(HotReloadConstants.CompilationListReasonCompilationFailed));
        }

        private void AssertRecompiledTargetGives(string reason, Action<HotReloadSavedReference> editTargetReference)
        {
            HotReloadCompilationAssemblyListStore store = SaveCleanThenEdit(saved =>
            {
                MarkRecompiled(saved);
                int edited = 0;
                foreach (HotReloadSavedReference reference in saved.references)
                {
                    if (Path.GetFileName(reference.path) == TargetReferenceFileName)
                    {
                        editTargetReference(reference);
                        edited++;
                    }
                }

                Assert.That(edited, Is.GreaterThan(0), "Precondition: the saved list must reference " + TargetReferenceFileName);
            });

            HotReloadCompilationListReconstruction result = Reconstruct(store);

            Assert.That(result.Assemblies, Is.Null, "list");
            Assert.That(result.FailureReason, Is.EqualTo(reason));
        }

        private HotReloadCompilationListRefill CreateRefill(HotReloadCompilationAssemblyListStore store, HotReloadCompilationAssemblyCache cache)
        {
            // Why inline: EditMode tests must not start pool work.
            return new HotReloadCompilationListRefill(
                store,
                cache,
                HotReloadSnapshotCaptureInputs.ReadOnMainThread,
                () => _isCompilationFailed,
                rebuild => Task.FromResult(rebuild()));
        }

        private HotReloadCompilationListReconstruction Reconstruct(HotReloadCompilationAssemblyListStore store)
        {
            return HotReloadCompilationAssemblyReconstruction.Reconstruct(
                store, HotReloadSnapshotCaptureInputs.ReadOnMainThread(), CancellationToken.None);
        }

        private HotReloadCompilationAssemblyListStore SaveCleanThenEdit(Action<HotReloadSavedCompilationList> edit)
        {
            HotReloadCompilationAssemblyListStore store = CreateStore();
            store.SaveInBackgroundIfClean(CompilationPipeline.GetAssemblies(), 0, _projectRoot);
            HotReloadSavedCompilationList saved = store.Load().List;
            Assert.That(saved, Is.Not.Null, "Precondition: the list must be saved.");
            edit(saved);
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(saved));
            return store;
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
                    _backgroundRuns++;
                    work();
                    return Task.CompletedTask;
                });
        }

        private static HotReloadSavedCompilationAssembly Target(HotReloadSavedCompilationList saved)
        {
            return saved.assemblies.Find(assembly => assembly.name == TargetAssemblyName);
        }

        // A stamp no file has, so the target goes through its PDB.
        private static HotReloadSavedCompilationAssembly MarkRecompiled(HotReloadSavedCompilationList saved)
        {
            HotReloadSavedCompilationAssembly target = Target(saved);
            target.dllStamp.lastWriteTicks = 1;
            return target;
        }

        private static int IndexOfReferenceNotIn(HotReloadSavedCompilationList saved, HotReloadSavedCompilationAssembly target)
        {
            HashSet<string> targetFileNames = new HashSet<string>();
            foreach (int index in target.compiledAssemblyReferenceIndexes)
            {
                targetFileNames.Add(Path.GetFileName(saved.references[index].path));
            }

            int found = saved.references.FindIndex(reference => !targetFileNames.Contains(Path.GetFileName(reference.path)));
            Assert.That(found, Is.GreaterThanOrEqualTo(0), "Precondition: some reference must be outside the target's.");
            return found;
        }

        private static int IndexOf(string assemblyName)
        {
            return Array.FindIndex(CompilationPipeline.GetAssemblies(), assembly => assembly.name == assemblyName);
        }

        private static UnityCompilationAssembly Find(UnityCompilationAssembly[] assemblies, string assemblyName)
        {
            return Array.Find(assemblies, assembly => assembly.name == assemblyName);
        }

        private static string[] NamesOf(UnityCompilationAssembly[] assemblies)
        {
            return Array.ConvertAll(assemblies, assembly => assembly.name);
        }

        private static string[] Append(string[] values, string value)
        {
            string[] appended = new string[values.Length + 1];
            values.CopyTo(appended, 0);
            appended[values.Length] = value;
            return appended;
        }

        private static int[] AppendIndex(int[] values, int value)
        {
            int[] appended = new int[values.Length + 1];
            values.CopyTo(appended, 0);
            appended[values.Length] = value;
            return appended;
        }
    }
}
