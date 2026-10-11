using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using NUnit.Framework;

using UnityEditor.Compilation;

using UnityEngine;
using UnityEngine.TestTools;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// EditMode coverage for the capture that reuses the previous build's snapshot directory and
    /// rewrites only the sources that changed, run in a per-test temporary project root. A previous
    /// copy holds a sentinel that no source holds, so a copy that still holds it was reused and a
    /// copy that holds the source's bytes was written again.
    /// </summary>
    public sealed class HotReloadIncrementalSnapshotCaptureTests
    {
        private const string AssemblyName = "Fixture";
        private const string TestAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const string PausePointsRuntimeAssemblyName = "UnityCLILoop.PausePoints.Runtime";
        private const string FixtureProjectRelativePath = "Assets/Tests/Editor/HotReload/HotReloadE2EFixtures.cs";
        private const string Sentinel = "PREVIOUS";
        private const uint RestoredDirectoryMode = 0x1ED; // 0755
        private const uint RestoredFileMode = 0x1A4; // 0644

        private static readonly DateTime BeforeTheCompileUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime CompileStartUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime SinceTheCompileUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private string _root;
        private string _previousDirectory;
        private string _newDirectory;
        private readonly List<string> _restoreDirectoryModes = new List<string>();
        private readonly List<string> _restoreFileModes = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "uloop-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
            Directory.CreateDirectory(Path.Combine(_root, HotReloadConstants.ScriptAssembliesRelativeDirectory));
            Directory.CreateDirectory(HotReloadSourceSnapshotLayout.Root(_root));
            _previousDirectory = SnapshotDirectory(Guid.NewGuid().ToString("N"));
            _newDirectory = SnapshotDirectory(Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_previousDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            // Why the modes come back first: a directory without write permission cannot be emptied.
            foreach (string path in _restoreDirectoryModes)
            {
                Chmod(path, RestoredDirectoryMode);
            }

            foreach (string path in _restoreFileModes)
            {
                Chmod(path, RestoredFileMode);
            }

            _restoreDirectoryModes.Clear();
            _restoreFileModes.Clear();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        /// <summary>
        /// Verifies a source with an unmarked line whose length and write time still match, written
        /// before the compile started, keeps the previous copy and its line.
        /// </summary>
        [Test]
        public void CaptureIncrementally_UnchangedTrustedSource_ReusesThePreviousCopy()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo(Sentinel));
            AssertNewLine("Assets/A.cs", false);
        }

        /// <summary>
        /// Verifies a source whose length or write time differs from its line is copied again.
        /// </summary>
        [TestCase(1, 0)]
        [TestCase(0, 1)]
        public void CaptureIncrementally_SourceWhoseLengthOrWriteTimeChanged_CopiesTheCurrentBytes(
            int lineLengthOffset,
            int lineSecondsOffset)
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            FileInfo source = SourceInfo("Assets/A.cs");
            WritePreviousManifest(HotReloadSourceStampManifest.FormatLine(
                SnapshotFileName("Assets/A.cs"),
                source.Length + lineLengthOffset,
                source.LastWriteTimeUtc.AddSeconds(-lineSecondsOffset).Ticks,
                false));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo("class A {}\n"));
            AssertNewLine("Assets/A.cs", false);
        }

        /// <summary>
        /// Verifies a source whose line marks it as edited after the compile is copied again, even
        /// though its length and write time match.
        /// </summary>
        [Test]
        public void CaptureIncrementally_SourceMarkedEditedAfterCompile_CopiesTheCurrentBytes()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", true));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo("class A {}\n"));
            AssertNewLine("Assets/A.cs", false);
        }

        /// <summary>
        /// Verifies a source the previous manifest has no line for is copied again, while a source
        /// beside it that has one is reused.
        /// </summary>
        [Test]
        public void CaptureIncrementally_SourceWithoutAManifestLine_CopiesTheCurrentBytes()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            WriteSource("Assets/B.cs", "class B {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            PlantPreviousCopy("Assets/B.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs", "Assets/B.cs");

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo(Sentinel), "A is reused");
            Assert.That(NewCopyText("Assets/B.cs"), Is.EqualTo("class B {}\n"), "B is copied");
            AssertNewLine("Assets/B.cs", false);
        }

        /// <summary>
        /// Verifies a source the previous build did not have is copied and gets a line.
        /// </summary>
        [Test]
        public void CaptureIncrementally_SourceNewToTheAssembly_CopiesIt()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            WriteSource("Assets/New.cs", "class New {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs", "Assets/New.cs");

            Assert.That(NewCopyText("Assets/New.cs"), Is.EqualTo("class New {}\n"));
            AssertNewLine("Assets/New.cs", false);
        }

        /// <summary>
        /// Verifies a source whose unmarked line still matches is copied again when the previous
        /// directory has no copy of it.
        /// </summary>
        [Test]
        public void CaptureIncrementally_PreviousCopyFileMissing_CopiesTheCurrentBytes()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo("class A {}\n"));
            AssertNewLine("Assets/A.cs", false);
        }

        /// <summary>
        /// Verifies a source whose unmarked line still matches, but whose write time falls inside the
        /// new compile's suspect window, is copied again and checked against the new PDB.
        /// </summary>
        [Test]
        public void CaptureIncrementally_SourceWrittenInsideTheNewSuspectWindow_IsCopiedAndCheckedAgain()
        {
            byte[] realBytes = File.ReadAllBytes(Path.Combine(RealProjectRoot(), FixtureProjectRelativePath));
            WriteSourceBytes(FixtureProjectRelativePath, realBytes, SinceTheCompileUtc);
            PlantPreviousCopy(FixtureProjectRelativePath);
            WritePreviousManifest(CurrentLine(FixtureProjectRelativePath, false));
            string dllPath = RealDllPath(TestAssemblyName);

            HotReloadSnapshotCaptureStats stats = CaptureIncrementally(
                CheckAgainst(dllPath, Path.ChangeExtension(dllPath, ".pdb")),
                FixtureProjectRelativePath);

            Assert.That(File.ReadAllBytes(NewCopyPath(FixtureProjectRelativePath)), Is.EqualTo(realBytes));
            Assert.That(stats.FilesChecked, Is.EqualTo(1));
            AssertNewLine(FixtureProjectRelativePath, false);
        }

        /// <summary>
        /// Verifies the copy of a source that is no longer in the assembly does not reach the new
        /// directory.
        /// </summary>
        [Test]
        public void CaptureIncrementally_SourceRemovedFromTheAssembly_DeletesItsCopy()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            WriteSource("Assets/Removed.cs", "class Removed {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            PlantPreviousCopy("Assets/Removed.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false), CurrentLine("Assets/Removed.cs", false));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(File.Exists(NewCopyPath("Assets/Removed.cs")), Is.False);
            Assert.That(NewManifest().TryGetStamp(SnapshotFileName("Assets/Removed.cs"), out long _, out long _), Is.False);
        }

        /// <summary>
        /// Verifies a listed source that no longer exists leaves neither its previous copy nor a line.
        /// </summary>
        [Test]
        public void CaptureIncrementally_SourceMissingNow_LeavesNoCopyAndNoLine()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            WriteSource("Assets/Gone.cs", "class Gone {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            PlantPreviousCopy("Assets/Gone.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false), CurrentLine("Assets/Gone.cs", false));
            File.Delete(SourcePath("Assets/Gone.cs"));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs", "Assets/Gone.cs");

            Assert.That(File.Exists(NewCopyPath("Assets/Gone.cs")), Is.False);
            Assert.That(NewManifest().TryGetStamp(SnapshotFileName("Assets/Gone.cs"), out long _, out long _), Is.False);
        }

        /// <summary>
        /// Verifies a capture whose stamp names the previous build's complete directory publishes the
        /// snapshot under the new MVID by moving that directory, so the reused copy is there, the
        /// previous directory is gone, and the stamp names the new MVID.
        /// </summary>
        [Test]
        public void CaptureIncrementally_PublishesUnderTheNewMvidAndThePreviousDirectoryIsGone()
        {
            string dllPath = PlantCompiledAssembly();
            string mvid = HotReloadAssemblyMvid.Read(dllPath);
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));
            WriteStampNamingPrevious();

            CaptureAssemblies("Assets/A.cs");

            string publishedDirectory = SnapshotDirectory(mvid);
            Assert.That(
                File.ReadAllText(HotReloadSourceSnapshotLayout.SourcePath(publishedDirectory, "Assets/A.cs")),
                Is.EqualTo(Sentinel));
            Assert.That(Directory.Exists(_previousDirectory), Is.False);
            Assert.That(File.ReadAllText(StampPath()).Split(',')[0], Is.EqualTo(mvid));
        }

        /// <summary>
        /// Verifies a temporary directory left over under the new name is deleted before the previous
        /// directory is moved there, so the capture still publishes and the leftover file is gone.
        /// </summary>
        [Test]
        public void CaptureIncrementally_WhenAnIncompleteDirectoryIsLeftOver_DeletesItFirst()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));
            string leftover = _newDirectory + HotReloadSourceSnapshotLayout.IncompleteDirectorySuffix;
            Directory.CreateDirectory(leftover);
            File.WriteAllText(Path.Combine(leftover, "stale.cs"), "stale\n");

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo(Sentinel));
            Assert.That(File.Exists(Path.Combine(_newDirectory, "stale.cs")), Is.False);
            Assert.That(Directory.Exists(leftover), Is.False);
        }

        /// <summary>
        /// Verifies that when the previous directory cannot be moved, the capture copies every source
        /// into the new directory instead and leaves the previous directory where it was.
        /// </summary>
        [Test]
        [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor })]
        public void CaptureIncrementally_WhenThePreviousDirectoryCannotBeMoved_FallsBackToAFullCopy()
        {
            IgnoreWhenRoot();
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            string lockedParent = Path.Combine(_root, "Locked");
            string previousDirectory = Path.Combine(lockedParent, AssemblyName + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(previousDirectory);
            File.WriteAllText(HotReloadSourceSnapshotLayout.SourcePath(previousDirectory, "Assets/A.cs"), Sentinel);
            HotReloadSourceStampManifest.Write(previousDirectory, new[] { CurrentLine("Assets/A.cs", false) });
            ChangeDirectoryMode(lockedParent, 0x16D); // 0555

            HotReloadSourceSnapshotCopier.CaptureIncrementally(
                _root,
                _newDirectory,
                previousDirectory,
                new[] { "Assets/A.cs" },
                AssemblyName,
                SuspectsNothing(),
                new HotReloadSnapshotCaptureStats());

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo("class A {}\n"));
            Assert.That(
                File.ReadAllText(HotReloadSourceSnapshotLayout.SourcePath(previousDirectory, "Assets/A.cs")),
                Is.EqualTo(Sentinel));
        }

        /// <summary>
        /// Verifies a stamp that names no usable previous snapshot makes the capture copy every source:
        /// a first field that is not an MVID, an MVID with no directory, a directory with no manifest,
        /// and a manifest with an older header.
        /// </summary>
        [TestCase("not_an_mvid")]
        [TestCase("no_directory")]
        [TestCase("no_manifest")]
        [TestCase("old_header")]
        public void CaptureAssemblies_WhenTheStampNamesAnUnusablePreviousDirectory_CopiesEverything(string unusable)
        {
            string dllPath = PlantCompiledAssembly();
            string mvid = HotReloadAssemblyMvid.Read(dllPath);
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            string line = CurrentLine("Assets/A.cs", false);
            string manifestPath = Path.Combine(_previousDirectory, HotReloadConstants.SourceStampManifestFileName);
            switch (unusable)
            {
                case "not_an_mvid":
                    WritePreviousManifest(line);
                    File.WriteAllText(StampPath(), "immutable,0,0");
                    break;
                case "no_directory":
                    WritePreviousManifest(line);
                    File.WriteAllText(StampPath(), Guid.NewGuid().ToString("N") + ",0,0");
                    break;
                case "no_manifest":
                    WriteStampNamingPrevious();
                    break;
                case "old_header":
                    File.WriteAllText(manifestPath, "uloop-source-stamps 1\n" + line + "\n");
                    WriteStampNamingPrevious();
                    break;
                default:
                    Assert.Fail("Unknown case " + unusable);
                    break;
            }

            CaptureAssemblies("Assets/A.cs");

            Assert.That(
                File.ReadAllText(HotReloadSourceSnapshotLayout.SourcePath(SnapshotDirectory(mvid), "Assets/A.cs")),
                Is.EqualTo("class A {}\n"));
        }

        /// <summary>
        /// Verifies that when the incremental snapshot cannot be published, the capture warns, the
        /// previous directory is gone and the stamp still names it, and the next capture copies every
        /// source.
        /// </summary>
        [Test]
        public void CaptureAssemblies_WhenPublishingAnIncrementalSnapshotFails_TheNextCaptureCopiesEverything()
        {
            string dllPath = PlantCompiledAssembly();
            string mvid = HotReloadAssemblyMvid.Read(dllPath);
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));
            WriteStampNamingPrevious();
            string stampBefore = File.ReadAllText(StampPath());
            string publishedDirectory = SnapshotDirectory(mvid);
            // A file where the published directory goes makes the final move fail.
            File.WriteAllText(publishedDirectory, "blocker");
            LogAssert.Expect(LogType.Warning, new Regex("Snapshot capture failed for assembly " + AssemblyName));

            CaptureAssemblies("Assets/A.cs");

            Assert.That(Directory.Exists(_previousDirectory), Is.False, "previous directory");
            Assert.That(File.ReadAllText(StampPath()), Is.EqualTo(stampBefore), "stamp");

            File.Delete(publishedDirectory);
            CaptureAssemblies("Assets/A.cs");

            Assert.That(
                File.ReadAllText(HotReloadSourceSnapshotLayout.SourcePath(publishedDirectory, "Assets/A.cs")),
                Is.EqualTo("class A {}\n"));
        }

        /// <summary>
        /// Verifies the capture reports the reused and the copied files: of three sources, the one that
        /// changed is copied and the other two are reused.
        /// </summary>
        [Test]
        public void CaptureAssemblies_ReportsReusedAndCopiedFiles()
        {
            PlantCompiledAssembly();
            string[] sources = { "Assets/A.cs", "Assets/B.cs", "Assets/C.cs" };
            List<string> lines = new List<string>();
            foreach (string source in sources)
            {
                WriteSource(source, "class X {}\n", BeforeTheCompileUtc);
                PlantPreviousCopy(source);
                lines.Add(CurrentLine(source, false));
            }

            WritePreviousManifest(lines.ToArray());
            WriteStampNamingPrevious();
            WriteSource("Assets/B.cs", "class Changed {}\n", BeforeTheCompileUtc.AddSeconds(1));

            HotReloadSnapshotCaptureStats stats = CaptureAssemblies(sources);

            Assert.That(stats.FilesReused, Is.EqualTo(2), "reused");
            Assert.That(stats.FilesCopied, Is.EqualTo(1), "copied");
        }

        /// <summary>
        /// Verifies a source that cannot be read but whose unmarked line still matches is reused
        /// without being read, keeping its line and raising no warning.
        /// </summary>
        [Test]
        [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor })]
        public void CaptureIncrementally_UnreadableSourceWhoseStampIsUnchanged_ReusesThePreviousCopyWithoutReadingIt()
        {
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));
            ChangeFileMode(SourcePath("Assets/A.cs"), 0);

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(NewCopyText("Assets/A.cs"), Is.EqualTo(Sentinel));
            AssertNewLine("Assets/A.cs", false);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Verifies a source that changed since its line and cannot be read leaves neither the previous
        /// copy nor a line, and the capture warns about it.
        /// </summary>
        [Test]
        [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor })]
        public void CaptureIncrementally_UnreadableSourceThatChanged_LeavesNoCopyAndNoLineAndWarns()
        {
            IgnoreWhenRoot();
            WriteSource("Assets/A.cs", "class A {}\n", BeforeTheCompileUtc);
            PlantPreviousCopy("Assets/A.cs");
            WritePreviousManifest(CurrentLine("Assets/A.cs", false));
            File.SetLastWriteTimeUtc(SourcePath("Assets/A.cs"), BeforeTheCompileUtc.AddSeconds(1));
            ChangeFileMode(SourcePath("Assets/A.cs"), 0);
            LogAssert.Expect(LogType.Warning, new Regex("Skipped 1 unreadable source"));

            CaptureIncrementally(SuspectsNothing(), "Assets/A.cs");

            Assert.That(File.Exists(NewCopyPath("Assets/A.cs")), Is.False);
            Assert.That(NewManifest().TryGetStamp(SnapshotFileName("Assets/A.cs"), out long _, out long _), Is.False);
        }

        /// <summary>
        /// Verifies that when the PDB check throws after the previous directory was moved to the new
        /// temporary directory, the exception reaches the caller and nothing is published: the
        /// previous directory now sits under the temporary name with the copy reused before the throw.
        /// </summary>
        [Test]
        public void CaptureIncrementally_WhenTheCheckThrowsAfterAdoptingThePreviousDirectory_PublishesNothing()
        {
            string mismatchedDirectory = Path.Combine(_root, "Mismatched");
            Directory.CreateDirectory(mismatchedDirectory);
            string dllPath = Path.Combine(mismatchedDirectory, "X.dll");
            string pdbPath = Path.Combine(mismatchedDirectory, "X.pdb");
            File.Copy(RealDllPath(TestAssemblyName), dllPath);
            File.Copy(Path.ChangeExtension(RealDllPath(PausePointsRuntimeAssemblyName), ".pdb"), pdbPath);
            WriteSource("Assets/Reused.cs", "class Reused {}\n", BeforeTheCompileUtc);
            WriteSource("Assets/Suspect.cs", "class Suspect {}\n", SinceTheCompileUtc);
            PlantPreviousCopy("Assets/Reused.cs");
            WritePreviousManifest(CurrentLine("Assets/Reused.cs", false));

            Assert.Throws<InvalidOperationException>(() => HotReloadSourceSnapshotCopier.CaptureIncrementally(
                _root,
                _newDirectory,
                _previousDirectory,
                new[] { "Assets/Reused.cs", "Assets/Suspect.cs" },
                AssemblyName,
                CheckAgainst(dllPath, pdbPath),
                new HotReloadSnapshotCaptureStats()));

            string temporaryDirectory = _newDirectory + HotReloadSourceSnapshotLayout.IncompleteDirectorySuffix;
            Assert.That(Directory.Exists(_newDirectory), Is.False, "published directory");
            Assert.That(Directory.Exists(_previousDirectory), Is.False, "previous directory");
            Assert.That(
                File.ReadAllText(HotReloadSourceSnapshotLayout.SourcePath(temporaryDirectory, "Assets/Reused.cs")),
                Is.EqualTo(Sentinel));
        }

        private HotReloadSnapshotCaptureStats CaptureIncrementally(HotReloadSnapshotSourceCheck check, params string[] sources)
        {
            HotReloadSnapshotCaptureStats stats = new HotReloadSnapshotCaptureStats();
            HotReloadSourceSnapshotCopier.CaptureIncrementally(
                _root,
                _newDirectory,
                _previousDirectory,
                sources,
                AssemblyName,
                check,
                stats);
            return stats;
        }

        private HotReloadSnapshotCaptureStats CaptureAssemblies(params string[] sources)
        {
            UnityCompilationAssembly assembly = new UnityCompilationAssembly(
                AssemblyName,
                HotReloadConstants.ScriptAssembliesRelativeDirectory + "/" + AssemblyName + HotReloadConstants.CompiledAssemblyExtension,
                sources,
                Array.Empty<string>(),
                Array.Empty<UnityCompilationAssembly>(),
                Array.Empty<string>(),
                AssemblyFlags.EditorAssembly);
            return HotReloadSourceSnapshotter.CaptureAssemblies(
                _root,
                new[] { assembly },
                HotReloadCompileStart.Unknown,
                new HotReloadPdbDocumentIndex(Path.Combine(_root, "PdbDocuments")));
        }

        // A check whose suspect window starts at the planted compile start, against the given build.
        private HotReloadSnapshotSourceCheck CheckAgainst(string dllPath, string pdbPath)
        {
            return new HotReloadSnapshotSourceCheck(
                CompileStartUtc.Ticks,
                dllPath,
                pdbPath,
                HotReloadAssemblyMvid.Read(dllPath),
                new HotReloadPdbDocumentIndex(Path.Combine(_root, "PdbDocuments")));
        }

        // A check whose suspect window never opens, so no copy is checked against a PDB.
        private HotReloadSnapshotSourceCheck SuspectsNothing()
        {
            return new HotReloadSnapshotSourceCheck(
                DateTime.MaxValue.Ticks,
                "unused.dll",
                "unused.pdb",
                "unused",
                new HotReloadPdbDocumentIndex(Path.Combine(_root, "PdbDocuments")));
        }

        // Copies this test assembly's own DLL and PDB, so the fixture has a real image for Cecil to read.
        private string PlantCompiledAssembly()
        {
            string dllPath = Path.Combine(
                _root,
                HotReloadConstants.ScriptAssembliesRelativeDirectory,
                AssemblyName + HotReloadConstants.CompiledAssemblyExtension);
            File.Copy(RealDllPath(TestAssemblyName), dllPath);
            File.Copy(Path.ChangeExtension(RealDllPath(TestAssemblyName), ".pdb"), Path.ChangeExtension(dllPath, ".pdb"));
            return dllPath;
        }

        // Why an mtime and length no DLL has: the stamp must not match, or the capture stops there.
        private void WriteStampNamingPrevious()
        {
            string previousMvid = Path.GetFileName(_previousDirectory).Substring(AssemblyName.Length + 1);
            File.WriteAllText(StampPath(), previousMvid + ",0,0");
        }

        private void WriteSource(string projectRelativePath, string text, DateTime lastWriteTimeUtc)
        {
            WriteSourceBytes(projectRelativePath, Encoding.ASCII.GetBytes(text), lastWriteTimeUtc);
        }

        // Why a write time in whole seconds: SetLastWriteTimeUtc stores microseconds while
        // LastWriteTimeUtc reports 100 ns ticks, so only such a value is read back exactly.
        private void WriteSourceBytes(string projectRelativePath, byte[] bytes, DateTime lastWriteTimeUtc)
        {
            string path = SourcePath(projectRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
            File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
            Assert.That(new FileInfo(path).LastWriteTimeUtc, Is.EqualTo(lastWriteTimeUtc), "write time of " + projectRelativePath);
        }

        private void PlantPreviousCopy(string projectRelativePath)
        {
            File.WriteAllText(HotReloadSourceSnapshotLayout.SourcePath(_previousDirectory, projectRelativePath), Sentinel);
        }

        private void WritePreviousManifest(params string[] lines)
        {
            HotReloadSourceStampManifest.Write(_previousDirectory, lines);
        }

        // The line the previous capture would have written for the source as it is now.
        private string CurrentLine(string projectRelativePath, bool editedAfterCompile)
        {
            FileInfo source = SourceInfo(projectRelativePath);
            return HotReloadSourceStampManifest.FormatLine(
                SnapshotFileName(projectRelativePath),
                source.Length,
                source.LastWriteTimeUtc.Ticks,
                editedAfterCompile);
        }

        private void AssertNewLine(string projectRelativePath, bool editedAfterCompile)
        {
            HotReloadSourceStampManifest manifest = NewManifest();
            FileInfo source = SourceInfo(projectRelativePath);
            Assert.That(
                manifest.TryGetStamp(SnapshotFileName(projectRelativePath), out long length, out long ticks),
                Is.True,
                "line of " + projectRelativePath);
            Assert.That(length, Is.EqualTo(source.Length), "length of " + projectRelativePath);
            Assert.That(ticks, Is.EqualTo(source.LastWriteTimeUtc.Ticks), "write time of " + projectRelativePath);
            Assert.That(manifest.IsEditedAfterCompile(SnapshotFileName(projectRelativePath)), Is.EqualTo(editedAfterCompile));
        }

        private HotReloadSourceStampManifest NewManifest()
        {
            return HotReloadSourceStampManifest.Load(_newDirectory);
        }

        private string NewCopyText(string projectRelativePath)
        {
            return File.ReadAllText(NewCopyPath(projectRelativePath));
        }

        private string NewCopyPath(string projectRelativePath)
        {
            return HotReloadSourceSnapshotLayout.SourcePath(_newDirectory, projectRelativePath);
        }

        private FileInfo SourceInfo(string projectRelativePath)
        {
            return new FileInfo(SourcePath(projectRelativePath));
        }

        private string SourcePath(string projectRelativePath)
        {
            return Path.Combine(_root, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private string SnapshotDirectory(string mvid)
        {
            return HotReloadSourceSnapshotLayout.AssemblyDirectory(_root, AssemblyName, mvid);
        }

        private string StampPath()
        {
            return Path.Combine(HotReloadSourceSnapshotLayout.Root(_root), AssemblyName + ".stamp");
        }

        private static string SnapshotFileName(string projectRelativePath)
        {
            return HotReloadSourceSnapshotLayout.SourceFileName(projectRelativePath);
        }

        private static string RealProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string RealDllPath(string assemblyName)
        {
            return Path.Combine(
                RealProjectRoot(),
                HotReloadConstants.ScriptAssembliesRelativeDirectory,
                assemblyName + HotReloadConstants.CompiledAssemblyExtension);
        }

        private void ChangeDirectoryMode(string path, uint mode)
        {
            _restoreDirectoryModes.Add(path);
            ChangeMode(path, mode);
        }

        private void ChangeFileMode(string path, uint mode)
        {
            _restoreFileModes.Add(path);
            ChangeMode(path, mode);
        }

        private static void ChangeMode(string path, uint mode)
        {
            if (Chmod(path, mode) != 0)
            {
                Assert.Fail("chmod failed for " + path + ": errno " + Marshal.GetLastWin32Error());
            }
        }

        // Why: the nightly EditMode run is root inside its container, and root ignores permission
        // bits, so a mode that stops this user stops nothing there. Ignore, not Assume: batchmode
        // -runTests exits with 2 on an inconclusive test even when nothing failed.
        private static void IgnoreWhenRoot()
        {
            if (GetEffectiveUserId() == 0)
            {
                Assert.Ignore("Permission bits do not stop root.");
            }
        }

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string path, uint mode);

        [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
        private static extern uint GetEffectiveUserId();
    }
}
