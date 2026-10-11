using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using UnityEditor.Compilation;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Stand-ins for the warm-up's context source and items, shared by the warm-up tests and by
    /// tests that build their own services graph.
    /// </summary>
    internal static class HotReloadWarmUpTestDoubles
    {
        internal const string TargetAssemblyName = "WarmUpTarget";

        /// <summary>A warm-up whose context source always skips, so it never touches a cache.</summary>
        internal static HotReloadWarmUp CreateInert()
        {
            return new HotReloadWarmUp(
                new FixedContextSource(HotReloadWarmUpCapture.Skipped(HotReloadWarmUpCapture.SkipReasonNoTargets)),
                Array.Empty<IHotReloadWarmUpItem>());
        }

        internal static HotReloadWarmUpCapture CreateReadyCapture(bool compilationListKept)
        {
            return HotReloadWarmUpCapture.Ready(
                new HotReloadWarmUpContext(
                    "project-root",
                    new[]
                    {
                        new HotReloadWarmUpTarget(
                            TargetAssemblyName,
                            TargetAssemblyName + ".dll",
                            TargetAssemblyName + ".pdb",
                            Array.Empty<string>())
                    },
                    compilationListKept));
        }

        /// <summary>The context of the only entry of <paramref name="operation"/> in the in-memory vibe log.</summary>
        internal static JObject ReadSingleVibeContext(string operation)
        {
            JArray entries = JArray.Parse(VibeLogger.GetLogsForAi(operation));
            NUnit.Framework.Assert.That(entries.Count, NUnit.Framework.Is.EqualTo(1), "entries of " + operation);
            return (JObject)entries[0]["context"];
        }

        /// <summary>The outcome names of the items of the only hot_reload_warm_up_complete entry, in order.</summary>
        internal static List<string> ReadCompletedOutcomes()
        {
            JObject context = ReadSingleVibeContext(HotReloadConstants.VibeLogWarmUpComplete);
            List<string> outcomes = new List<string>();
            foreach (JToken item in (JArray)context["items"])
            {
                outcomes.Add((string)item["name"] + ":" + (string)item["outcome"]);
            }

            return outcomes;
        }
    }

    /// <summary>A context source that returns the same capture every time.</summary>
    internal sealed class FixedContextSource : IHotReloadWarmUpContextSource
    {
        private readonly HotReloadWarmUpCapture _capture;

        internal FixedContextSource(HotReloadWarmUpCapture capture)
        {
            _capture = capture;
        }

        internal int CaptureCount { get; private set; }

        public HotReloadWarmUpCapture Capture()
        {
            CaptureCount++;
            return _capture;
        }
    }

    /// <summary>An item that appends its name to a shared list when it runs and then completes.</summary>
    internal sealed class RecordingWarmUpItem : IHotReloadWarmUpItem
    {
        private readonly List<string> _ran;

        internal RecordingWarmUpItem(string name, List<string> ran)
        {
            Name = name;
            _ran = ran;
        }

        public string Name { get; }

        public bool NeedsCompilationList => false;

        public Task RunAsync(HotReloadWarmUpContext context, CancellationToken ct)
        {
            _ran.Add(Name);
            return Task.CompletedTask;
        }
    }

    /// <summary>An item that appends its name when it runs and completes only when the test completes its source.</summary>
    internal sealed class PendingWarmUpItem : IHotReloadWarmUpItem
    {
        private readonly List<string> _ran;

        internal PendingWarmUpItem(string name, List<string> ran)
        {
            Name = name;
            _ran = ran;
        }

        public string Name { get; }

        public bool NeedsCompilationList => false;

        internal TaskCompletionSource<bool> Release { get; } = new TaskCompletionSource<bool>();

        public Task RunAsync(HotReloadWarmUpContext context, CancellationToken ct)
        {
            _ran.Add(Name);
            return Release.Task;
        }
    }

    /// <summary>
    /// An item of two units: it appends its name for the first, waits until the test completes
    /// its source, checks the token between the units as production items do, and appends its
    /// name with "#2" for the second.
    /// </summary>
    internal sealed class TwoUnitWarmUpItem : IHotReloadWarmUpItem
    {
        private readonly List<string> _ran;

        internal TwoUnitWarmUpItem(string name, List<string> ran)
        {
            Name = name;
            _ran = ran;
        }

        public string Name { get; }

        public bool NeedsCompilationList => false;

        internal TaskCompletionSource<bool> BetweenUnits { get; } = new TaskCompletionSource<bool>();

        public async Task RunAsync(HotReloadWarmUpContext context, CancellationToken ct)
        {
            _ran.Add(Name);
            await BetweenUnits.Task;
            ct.ThrowIfCancellationRequested();
            _ran.Add(Name + "#2");
        }
    }

    /// <summary>
    /// An item that needs the compilation assembly list: it looks the target up through the given
    /// lookup and appends its name when it runs.
    /// </summary>
    internal sealed class ListLookupWarmUpItem : IHotReloadWarmUpItem
    {
        private readonly List<string> _ran;
        private readonly Func<string, UnityCompilationAssembly> _findByName;

        internal ListLookupWarmUpItem(string name, List<string> ran, Func<string, UnityCompilationAssembly> findByName)
        {
            Name = name;
            _ran = ran;
            _findByName = findByName;
        }

        public string Name { get; }

        public bool NeedsCompilationList => true;

        public Task RunAsync(HotReloadWarmUpContext context, CancellationToken ct)
        {
            _findByName(HotReloadWarmUpTestDoubles.TargetAssemblyName);
            _ran.Add(Name);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The fetch of a compilation assembly cache, counting the times the cache asks Unity for the list.
    /// </summary>
    internal sealed class CountingCompilationListFetch
    {
        internal int Count { get; private set; }

        internal HotReloadCompilationAssemblyCache CreateEmptyCache()
        {
            return new HotReloadCompilationAssemblyCache(Fetch);
        }

        /// <summary>A cache that already keeps Unity's list, as after a rebuild that was kept.</summary>
        internal HotReloadCompilationAssemblyCache CreateFilledCache()
        {
            HotReloadCompilationAssemblyCache cache = CreateEmptyCache();
            NUnit.Framework.Assert.That(cache.TryFill(CompilationPipeline.GetAssemblies(), cache.Generation), NUnit.Framework.Is.True);
            return cache;
        }

        private UnityCompilationAssembly[] Fetch()
        {
            Count++;
            return CompilationPipeline.GetAssemblies();
        }
    }

    /// <summary>An item whose task faults with the given exception.</summary>
    internal sealed class ThrowingWarmUpItem : IHotReloadWarmUpItem
    {
        private readonly Exception _exception;

        internal ThrowingWarmUpItem(string name, Exception exception)
        {
            Name = name;
            _exception = exception;
        }

        public string Name { get; }

        public bool NeedsCompilationList => false;

        public Task RunAsync(HotReloadWarmUpContext context, CancellationToken ct)
        {
            return Task.FromException(_exception);
        }
    }
}
