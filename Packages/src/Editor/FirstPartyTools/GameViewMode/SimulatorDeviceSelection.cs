using System;
using System.Collections.Generic;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// How a requested device name related to the Simulator's device list.
    /// </summary>
    internal enum SimulatorDeviceMatchKind
    {
        Selected,
        Ambiguous,
        NotFound,
    }

    /// <summary>
    /// Outcome of matching a requested device name: the one device it names, the devices it could
    /// equally name, or nothing.
    /// </summary>
    internal readonly struct SimulatorDeviceSelectionResult
    {
        private SimulatorDeviceSelectionResult(SimulatorDeviceMatchKind kind, string deviceName, IReadOnlyList<string> candidates)
        {
            Kind = kind;
            DeviceName = deviceName;
            Candidates = candidates;
        }

        public SimulatorDeviceMatchKind Kind { get; }

        /// <summary>The matched device name. Non-null only when <see cref="Kind"/> is Selected.</summary>
        public string DeviceName { get; }

        /// <summary>The tied device names in list order when <see cref="Kind"/> is Ambiguous; empty otherwise.</summary>
        public IReadOnlyList<string> Candidates { get; }

        public static SimulatorDeviceSelectionResult Selected(string deviceName)
        {
            return new SimulatorDeviceSelectionResult(SimulatorDeviceMatchKind.Selected, deviceName, Array.Empty<string>());
        }

        public static SimulatorDeviceSelectionResult Ambiguous(IReadOnlyList<string> candidates)
        {
            return new SimulatorDeviceSelectionResult(SimulatorDeviceMatchKind.Ambiguous, null, candidates);
        }

        public static SimulatorDeviceSelectionResult NotFound()
        {
            return new SimulatorDeviceSelectionResult(SimulatorDeviceMatchKind.NotFound, null, Array.Empty<string>());
        }
    }

    /// <summary>
    /// Matches a device name typed on the command line against the Device Simulator's device list,
    /// case-insensitively: a whole-name match wins, then a name that ends with the given words, then
    /// any name containing them.
    /// </summary>
    internal static class SimulatorDeviceSelection
    {
        /// <summary>
        /// Precondition: query is not blank and names is not null.
        /// </summary>
        public static SimulatorDeviceSelectionResult Resolve(string query, IReadOnlyList<string> names)
        {
            Debug.Assert(names != null, "names must not be null");
            string wanted = query.Trim();
            Debug.Assert(wanted.Length > 0, "query must not be blank");

            SimulatorDeviceSelectionResult? exact = Decide(Collect(names, name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)));
            if (exact.HasValue)
            {
                return exact.Value;
            }

            // The leading space keeps "Pixel 5" from end-matching "Google Pixel 15". Device names are
            // "<vendor> <model> <variant>", so an end match singles out the base model the user named.
            string suffix = " " + wanted;
            SimulatorDeviceSelectionResult? endsWith = Decide(Collect(names, name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
            if (endsWith.HasValue)
            {
                return endsWith.Value;
            }

            SimulatorDeviceSelectionResult? contains = Decide(Collect(names, name => name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0));
            if (contains.HasValue)
            {
                return contains.Value;
            }

            return SimulatorDeviceSelectionResult.NotFound();
        }

        private static List<string> Collect(IReadOnlyList<string> names, Func<string, bool> matches)
        {
            List<string> found = new();
            foreach (string name in names)
            {
                if (name != null && matches(name))
                {
                    found.Add(name);
                }
            }

            return found;
        }

        // A tier with no match yields null so the next, looser tier is tried.
        private static SimulatorDeviceSelectionResult? Decide(List<string> matches)
        {
            if (matches.Count == 1)
            {
                return SimulatorDeviceSelectionResult.Selected(matches[0]);
            }

            if (matches.Count > 1)
            {
                return SimulatorDeviceSelectionResult.Ambiguous(matches);
            }

            return null;
        }
    }
}
