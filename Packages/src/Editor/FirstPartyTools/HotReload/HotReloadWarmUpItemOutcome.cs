using System;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// How one warm-up item ended, as reported in the hot_reload_warm_up_complete entry.
    /// </summary>
    internal readonly struct HotReloadWarmUpItemOutcome
    {
        private HotReloadWarmUpItemOutcome(string name, long ms, HotReloadWarmUpItemOutcomeKind kind, string detail)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "name must not be null or empty.");
            Name = name;
            Ms = ms;
            Kind = kind;
            Detail = detail;
        }

        internal string Name { get; }

        internal long Ms { get; }

        internal HotReloadWarmUpItemOutcomeKind Kind { get; }

        /// <summary>
        /// The exception's type and message for a failed item, the reason for a skipped one; empty otherwise.
        /// </summary>
        internal string Detail { get; }

        internal static HotReloadWarmUpItemOutcome Done(string name, long ms)
        {
            return new HotReloadWarmUpItemOutcome(name, ms, HotReloadWarmUpItemOutcomeKind.Done, string.Empty);
        }

        /// <summary>An item that never started (<paramref name="ms"/> 0) or stopped between its units.</summary>
        internal static HotReloadWarmUpItemOutcome Cancelled(string name, long ms)
        {
            return new HotReloadWarmUpItemOutcome(name, ms, HotReloadWarmUpItemOutcomeKind.Cancelled, string.Empty);
        }

        /// <summary>An item that was not run because what it needs is unavailable.</summary>
        internal static HotReloadWarmUpItemOutcome Skipped(string name, string reason)
        {
            Debug.Assert(!string.IsNullOrEmpty(reason), "reason must not be null or empty.");
            return new HotReloadWarmUpItemOutcome(name, 0, HotReloadWarmUpItemOutcomeKind.Skipped, reason);
        }

        internal static HotReloadWarmUpItemOutcome Failed(string name, long ms, Exception exception)
        {
            Debug.Assert(exception != null, "exception must not be null.");
            return new HotReloadWarmUpItemOutcome(
                name,
                ms,
                HotReloadWarmUpItemOutcomeKind.Failed,
                exception.GetType().Name + ": " + exception.Message);
        }
    }

    internal enum HotReloadWarmUpItemOutcomeKind
    {
        Done,
        Cancelled,
        Failed,
        Skipped
    }
}
