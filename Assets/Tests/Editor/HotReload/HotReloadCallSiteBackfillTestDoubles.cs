using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// A dll loader the test controls: it records each path it was asked for, completes at once
    /// for most paths, holds the ones in <see cref="PendingFor"/> until the test releases them,
    /// and throws for the ones in <see cref="ThrowFor"/> or <see cref="ThrowUnexpectedFor"/>.
    /// Why most paths complete at once: a backfill whose cancel does not work would then read
    /// the next path and fail an assertion, instead of waiting forever for a held one.
    /// </summary>
    internal sealed class PendingDllLoader
    {
        private readonly Dictionary<string, TaskCompletionSource<bool>> _pending =
            new Dictionary<string, TaskCompletionSource<bool>>(StringComparer.Ordinal);

        internal List<string> Ran { get; } = new List<string>();

        internal HashSet<string> PendingFor { get; } = new HashSet<string>(StringComparer.Ordinal);

        internal HashSet<string> ThrowFor { get; } = new HashSet<string>(StringComparer.Ordinal);

        internal HashSet<string> ThrowUnexpectedFor { get; } = new HashSet<string>(StringComparer.Ordinal);

        internal Task Load(string dllPath, CancellationToken ct)
        {
            Ran.Add(dllPath);
            if (ThrowFor.Contains(dllPath))
            {
                throw new IOException("test: cannot read " + dllPath);
            }

            if (ThrowUnexpectedFor.Contains(dllPath))
            {
                throw new InvalidOperationException("unexpected");
            }

            if (!PendingFor.Contains(dllPath))
            {
                return Task.CompletedTask;
            }

            return GetOrCreate(dllPath).Task;
        }

        internal void Release(string dllPath)
        {
            GetOrCreate(dllPath).TrySetResult(true);
        }

        internal void Fail(string dllPath, Exception error)
        {
            GetOrCreate(dllPath).TrySetException(error);
        }

        internal void ReleaseAll()
        {
            foreach (TaskCompletionSource<bool> source in _pending.Values)
            {
                source.TrySetResult(true);
            }
        }

        private TaskCompletionSource<bool> GetOrCreate(string dllPath)
        {
            if (!_pending.TryGetValue(dllPath, out TaskCompletionSource<bool> source))
            {
                source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending.Add(dllPath, source);
            }

            return source;
        }
    }

    /// <summary>
    /// A stand-in for the backfill's recompute of the refused dlls: it counts its calls and
    /// answers each from a function of the call number (1-based) and the backfill's token.
    /// </summary>
    internal sealed class RefusedAgainFinder
    {
        private readonly Func<int, CancellationToken, IReadOnlyList<string>> _answer;

        internal RefusedAgainFinder(Func<int, CancellationToken, IReadOnlyList<string>> answer)
        {
            _answer = answer;
        }

        internal int Calls { get; private set; }

        /// <summary>Answers the given lists in order, then empty lists.</summary>
        internal static RefusedAgainFinder Answering(params string[][] answers)
        {
            return new RefusedAgainFinder(
                (call, ct) => call <= answers.Length ? answers[call - 1] : Array.Empty<string>());
        }

        internal Task<IReadOnlyList<string>> Find(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_answer(Calls, ct));
        }
    }

    internal static class HotReloadCallSiteBackfillTestDoubles
    {
        /// <summary>A recompute that finds no refused dll, for tests that do not exercise it.</summary>
        internal static Task<IReadOnlyList<string>> FindNothingRefusedAgain(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }
}
