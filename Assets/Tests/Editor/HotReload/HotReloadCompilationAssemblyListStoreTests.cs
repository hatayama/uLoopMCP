using System;
using System.IO;
using System.Threading.Tasks;

using NUnit.Framework;

using UnityEditor.Compilation;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Contract of when the compilation assembly list is saved for the next domain (only the first
    /// fetch of a domain without an invalidation and without a failed compile, and only when no
    /// invalidation happened before the file is published, because only then does the list
    /// describe the dlls on disk) and of telling a missing saved list from an unusable one when it
    /// is loaded.
    /// </summary>
    public sealed class HotReloadCompilationAssemblyListStoreTests
    {
        private const string TestAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const string MissingReferencePath = "Packages/com.example.missing/Plugins/Missing.dll";
        private const string PreviousFileContent = "previous";

        // A complete saved file: one assembly whose only reference is the one reference entry.
        // Each unreadable case below changes exactly one part of it.
        private const string CompleteFile =
            "{\"formatVersion\":1,"
            + "\"assemblies\":[{\"name\":\"A\",\"outputPath\":\"Library/ScriptAssemblies/A.dll\",\"sourceFiles\":[],"
            + "\"defines\":[],\"assemblyReferences\":[],\"compiledAssemblyReferenceIndexes\":[0],\"dllStamp\":null}],"
            + "\"references\":[{\"path\":\"Plugins/R.dll\",\"stamp\":{\"length\":1,\"lastWriteTicks\":2},"
            + "\"mvid\":\"0123456789abcdef0123456789abcdef\"}]}";

        private string _projectRoot;
        private string _filePath;
        private string _referencePath;
        private bool _isCompilationFailed;
        private int _currentGeneration;
        private int _backgroundRuns;
        private Action _deferredWork;

        [SetUp]
        public void SetUp()
        {
            _projectRoot = Path.Combine(Path.GetTempPath(), "uloop-compilation-list-store-" + Guid.NewGuid().ToString("N"));
            string scriptAssemblies = Path.Combine(_projectRoot, HotReloadConstants.ScriptAssembliesRelativeDirectory);
            Directory.CreateDirectory(scriptAssemblies);
            File.WriteAllText(Path.Combine(scriptAssemblies, "Saved" + HotReloadConstants.CompiledAssemblyExtension), "dll");
            _filePath = Path.Combine(_projectRoot, HotReloadConstants.CompilationAssemblyListRelativePath);
            _referencePath = CompiledAssemblyLayout.Resolve(RealProjectRoot()).DllPath(TestAssemblyName);
            _isCompilationFailed = false;
            _currentGeneration = 0;
            _backgroundRuns = 0;
            _deferredWork = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_projectRoot))
            {
                Directory.Delete(_projectRoot, true);
            }
        }

        /// <summary>
        /// What: a clean first fetch is saved with the read fields and the dll stamp. Precompiled
        /// references are saved once in the references table, with stamp and MVID, or with neither
        /// when the file is missing, and assemblies point at them by index in Unity's order.
        /// </summary>
        [Test]
        public void Store_SavesTheFirstFetchOfACleanDomain()
        {
            HotReloadCompilationAssemblyListStore store = CreateStore();

            store.SaveInBackgroundIfClean(CreateFetchedList(), 0, _projectRoot);

            HotReloadSavedCompilationListLoad load = store.Load();
            Assert.That(load.FailureReason, Is.Null);
            HotReloadSavedCompilationList saved = load.List;
            Assert.That(saved.assemblies.Count, Is.EqualTo(2));
            HotReloadSavedCompilationAssembly entry = saved.assemblies[1];
            Assert.That(entry.name, Is.EqualTo("Saved"));
            Assert.That(entry.sourceFiles, Is.EqualTo(new[] { "Assets/Saved/A.cs", "Assets/Saved/B.cs" }));
            Assert.That(entry.defines, Is.EqualTo(new[] { "SECOND", "FIRST" }));
            Assert.That(entry.assemblyReferences, Is.EqualTo(new[] { "Referenced" }));
            Assert.That(entry.compiledAssemblyReferenceIndexes, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(saved.assemblies[0].compiledAssemblyReferenceIndexes, Is.EqualTo(new[] { 0 }));
            Assert.That(entry.dllStamp, Is.Not.Null);
            Assert.That(saved.assemblies[0].dllStamp, Is.Null, "Referenced has no dll in the temporary project.");
            Assert.That(saved.references.Count, Is.EqualTo(2));
            Assert.That(saved.references[0].path, Is.EqualTo(_referencePath));
            Assert.That(saved.references[0].mvid, Is.EqualTo(HotReloadAssemblyMvid.Read(_referencePath)));
            Assert.That(saved.references[1].path, Is.EqualTo(MissingReferencePath));
            Assert.That(saved.references[1].stamp, Is.Null);
            Assert.That(saved.references[1].mvid, Is.Null);
        }

        /// <summary>
        /// What: a fetch after an invalidation of the domain (a compile start, or a script set change
        /// without a reload) is not saved.
        /// </summary>
        [Test]
        public void Store_DoesNotSaveAFetchAfterAnInvalidation()
        {
            HotReloadCompilationAssemblyListStore store = CreateStore();

            store.SaveInBackgroundIfClean(CreateFetchedList(), 1, _projectRoot);

            Assert.That(File.Exists(_filePath), Is.False);
            Assert.That(_backgroundRuns, Is.EqualTo(0));
        }

        /// <summary>
        /// What: a fetch while the last compile failed is not saved, since the list can name sources
        /// the dlls were not built from.
        /// </summary>
        [Test]
        public void Store_DoesNotSaveWhileCompilationFailed()
        {
            _isCompilationFailed = true;
            HotReloadCompilationAssemblyListStore store = CreateStore();

            store.SaveInBackgroundIfClean(CreateFetchedList(), 0, _projectRoot);

            Assert.That(File.Exists(_filePath), Is.False);
            Assert.That(_backgroundRuns, Is.EqualTo(0));
        }

        /// <summary>
        /// What: when an invalidation happens after the hand-off but before the background write
        /// publishes, the earlier saved file is left as it was and no temporary file remains.
        /// </summary>
        [Test]
        public void Store_WhenInvalidatedBeforeTheWriteFinishes_KeepsThePreviousFile()
        {
            WriteSavedFile(PreviousFileContent);
            HotReloadCompilationAssemblyListStore store = CreateDeferringStore();
            store.SaveInBackgroundIfClean(CreateFetchedList(), 0, _projectRoot);

            _currentGeneration = 1;
            _deferredWork();

            Assert.That(File.ReadAllText(_filePath), Is.EqualTo(PreviousFileContent));
            Assert.That(File.Exists(_filePath + ".tmp"), Is.False);
        }

        /// <summary>
        /// What: without a saved file, the load names no_saved_list.
        /// </summary>
        [Test]
        public void Load_WithoutASavedFile_ReportsNoSavedList()
        {
            HotReloadSavedCompilationListLoad load = CreateStore().Load();

            Assert.That(load.List, Is.Null);
            Assert.That(load.FailureReason, Is.EqualTo(HotReloadConstants.CompilationListReasonNoSavedList));
        }

        /// <summary>
        /// What: a saved file with every required field, in-range indexes and the current format
        /// version loads. The unreadable cases below each differ from it in one way.
        /// </summary>
        [Test]
        public void Load_CompleteFileOfTheCurrentVersion_ReturnsTheList()
        {
            WriteSavedFile(CompleteFile);

            HotReloadSavedCompilationListLoad load = CreateStore().Load();

            Assert.That(load.FailureReason, Is.Null);
            Assert.That(load.List.assemblies[0].compiledAssemblyReferenceIndexes, Is.EqualTo(new[] { 0 }));
        }

        /// <summary>
        /// What: a torn file loads as saved_list_unreadable instead of throwing.
        /// </summary>
        [Test]
        public void Load_TornFile_ReportsUnreadable()
        {
            WriteSavedFile(CompleteFile.Substring(0, CompleteFile.Length / 2));

            HotReloadSavedCompilationListLoad load = CreateStore().Load();

            Assert.That(load.List, Is.Null);
            Assert.That(load.FailureReason, Is.EqualTo(HotReloadConstants.CompilationListReasonSavedListUnreadable));
        }

        /// <summary>
        /// What: a file of another format version, an assembly without a name, a reference index
        /// outside the table and a reference with a stamp but no MVID all load as
        /// saved_list_unreadable instead of failing later in the rebuild.
        /// </summary>
        [TestCase("\"formatVersion\":1", "\"formatVersion\":2", TestName = "Load_OtherFormatVersion_ReportsUnreadable")]
        [TestCase("\"name\":\"A\",", "", TestName = "Load_AssemblyWithoutAName_ReportsUnreadable")]
        [TestCase("\"compiledAssemblyReferenceIndexes\":[0]", "\"compiledAssemblyReferenceIndexes\":[1]", TestName = "Load_ReferenceIndexOutOfRange_ReportsUnreadable")]
        [TestCase(",\"mvid\":\"0123456789abcdef0123456789abcdef\"", "", TestName = "Load_ReferenceWithAStampButNoMvid_ReportsUnreadable")]
        public void Load_IncompleteFile_ReportsUnreadable(string completePart, string replacement)
        {
            Assert.That(CompleteFile, Does.Contain(completePart), "The case must change the complete file.");
            WriteSavedFile(CompleteFile.Replace(completePart, replacement));

            HotReloadSavedCompilationListLoad load = CreateStore().Load();

            Assert.That(load.List, Is.Null);
            Assert.That(load.FailureReason, Is.EqualTo(HotReloadConstants.CompilationListReasonSavedListUnreadable));
        }

        private void WriteSavedFile(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            File.WriteAllText(_filePath, json);
        }

        private HotReloadCompilationAssemblyListStore CreateStore()
        {
            // Why inline: EditMode tests must not start pool work, so the "background" runs here.
            return new HotReloadCompilationAssemblyListStore(
                _filePath,
                () => _isCompilationFailed,
                () => _currentGeneration,
                work =>
                {
                    _backgroundRuns++;
                    work();
                    return Task.CompletedTask;
                });
        }

        private HotReloadCompilationAssemblyListStore CreateDeferringStore()
        {
            // Keeps the write so the test can change the generation between the hand-off and the write.
            return new HotReloadCompilationAssemblyListStore(
                _filePath,
                () => _isCompilationFailed,
                () => _currentGeneration,
                work =>
                {
                    _deferredWork = work;
                    return Task.CompletedTask;
                });
        }

        private UnityCompilationAssembly[] CreateFetchedList()
        {
            UnityCompilationAssembly referenced = new UnityCompilationAssembly(
                "Referenced",
                HotReloadConstants.ScriptAssembliesRelativeDirectory + "/Referenced" + HotReloadConstants.CompiledAssemblyExtension,
                new[] { "Assets/Referenced/R.cs" },
                Array.Empty<string>(),
                Array.Empty<UnityCompilationAssembly>(),
                new[] { _referencePath },
                AssemblyFlags.EditorAssembly);
            UnityCompilationAssembly saved = new UnityCompilationAssembly(
                "Saved",
                HotReloadConstants.ScriptAssembliesRelativeDirectory + "/Saved" + HotReloadConstants.CompiledAssemblyExtension,
                new[] { "Assets/Saved/A.cs", "Assets/Saved/B.cs" },
                new[] { "SECOND", "FIRST" },
                new[] { referenced },
                new[] { MissingReferencePath, _referencePath },
                AssemblyFlags.EditorAssembly);
            return new[] { referenced, saved };
        }

        private static string RealProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
