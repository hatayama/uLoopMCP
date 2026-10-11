using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Debug = UnityEngine.Debug;

using io.github.hatayama.UnityCliLoop.ToolContracts;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Saves the compilation assembly list of a clean domain so the next domain can rebuild it
    /// without asking Unity on the main thread, and loads it back.
    /// </summary>
    internal sealed class HotReloadCompilationAssemblyListStore
    {
        internal const int FormatVersion = 1;
        private const string TempFileExtension = ".tmp";

        private readonly string _filePath;
        private readonly Func<bool> _isCompilationFailed;
        private readonly Func<int> _currentGeneration;
        private readonly Func<Action, Task> _runInBackground;

        internal HotReloadCompilationAssemblyListStore(
            string filePath,
            Func<bool> isCompilationFailed,
            Func<int> currentGeneration,
            Func<Action, Task> runInBackground)
        {
            Debug.Assert(!string.IsNullOrEmpty(filePath), "filePath must not be null or empty.");
            Debug.Assert(isCompilationFailed != null, "isCompilationFailed must not be null.");
            Debug.Assert(currentGeneration != null, "currentGeneration must not be null.");
            Debug.Assert(runInBackground != null, "runInBackground must not be null.");
            _filePath = filePath;
            _isCompilationFailed = isCompilationFailed;
            _currentGeneration = currentGeneration;
            _runInBackground = runInBackground;
        }

        /// <summary>
        /// Saves the list in the background when it describes the dlls on disk: no invalidation has
        /// happened in this domain (generation 0) and the last compile did not fail. Otherwise the
        /// list can name sources that were never compiled, and a later domain whose dlls did not
        /// change would trust it. Main thread only, because it reads the compile state.
        /// </summary>
        internal void SaveInBackgroundIfClean(UnityCompilationAssembly[] fetched, int generation, string projectRoot)
        {
            Debug.Assert(fetched != null && fetched.Length > 0, "fetched must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");

            if (generation != 0)
            {
                LogSaveSkipped("generation");
                return;
            }

            if (_isCompilationFailed())
            {
                LogSaveSkipped("compilation_failed");
                return;
            }

            // Why only logged: the saved list is an optimization, and without it the next domain
            // asks Unity as it always did.
            _runInBackground(() => Write(fetched, projectRoot, generation))
                .ContinueWith(
                    LogSaveFailure,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        /// <summary>
        /// Loads the saved list. When it cannot be used, the result has no list and names why: no
        /// file, or a file that is torn, of another format version, or missing a required field.
        /// Touches only the file, so it can run on a pool thread.
        /// </summary>
        internal HotReloadSavedCompilationListLoad Load()
        {
            if (!File.Exists(_filePath))
            {
                return HotReloadSavedCompilationListLoad.Failed(HotReloadConstants.CompilationListReasonNoSavedList);
            }

            string json = File.ReadAllText(_filePath, Encoding.UTF8);
            HotReloadSavedCompilationList saved;
            // Why caught: the file is outside input that a crash can tear or a person can edit, and an
            // unreadable file only sends the next domain back to Unity's list. Only JSON errors are
            // caught; an IO error goes to the caller's boundary.
            try
            {
                saved = JsonConvert.DeserializeObject<HotReloadSavedCompilationList>(json);
            }
            catch (JsonException)
            {
                return HotReloadSavedCompilationListLoad.Failed(HotReloadConstants.CompilationListReasonSavedListUnreadable);
            }

            // Why the fields are checked here: a missing one would otherwise surface later as a
            // NullReferenceException that names no cause.
            if (saved == null || saved.formatVersion != FormatVersion || !saved.IsComplete())
            {
                return HotReloadSavedCompilationListLoad.Failed(HotReloadConstants.CompilationListReasonSavedListUnreadable);
            }

            return HotReloadSavedCompilationListLoad.Loaded(saved);
        }

        private void Write(UnityCompilationAssembly[] fetched, string projectRoot, int generation)
        {
            Stopwatch watch = Stopwatch.StartNew();
            HotReloadSavedCompilationList saved = HotReloadSavedCompilationList.Capture(fetched, projectRoot, FormatVersion);
            string json = JsonConvert.SerializeObject(saved);
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

            // Why a fixed temp name: a domain has one writer, and the previous domain's writer
            // stopped when that domain was unloaded.
            string tempPath = _filePath + TempFileExtension;
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            // Why the generation is read again here: a compile that started after the hand-off may
            // have written new dlls whose stamps Capture read, which would pair the old list with
            // them. Unity writes dlls only after compilationStarted invalidates the cache, so an
            // unchanged generation means every stamp read above belongs to this domain's dlls.
            if (_currentGeneration() != generation)
            {
                File.Delete(tempPath);
                LogSaveSkipped("superseded");
                return;
            }

            long bytes = new FileInfo(tempPath).Length;
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            File.Move(tempPath, _filePath);
            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogCompilationListSaved,
                "Hot reload saved the compilation assembly list for the next domain.",
                new
                {
                    bytes,
                    saveMs = watch.ElapsedMilliseconds,
                    assemblies = saved.assemblies.Count,
                    references = saved.references.Count,
                });
        }

        private static void LogSaveSkipped(string reason)
        {
            // Why logged: domains that never save would silently keep the next startup on Unity's list.
            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogCompilationListSaveSkipped,
                "Hot reload did not save the compilation assembly list of this domain.",
                new { reason });
        }

        private static void LogSaveFailure(Task write)
        {
            if (!write.IsFaulted)
            {
                return;
            }

            // Why read before the call: VibeLogger.LogWarning is compiled out without ULOOP_DEBUG,
            // and reading Exception is what marks the fault as observed.
            Exception error = write.Exception.GetBaseException();
            VibeLogger.LogWarning(
                HotReloadConstants.VibeLogCompilationListSaveFailed,
                "Hot reload could not save the compilation assembly list.",
                new { error = error.GetType().Name });
        }
    }
}
