using System.Collections.Generic;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// One line of a surface description and the row it belongs to.
    /// </summary>
    internal readonly struct SurfaceEntry
    {
        public SurfaceEntry(string text, SurfaceOwner owner)
        {
            Text = text;
            Owner = owner;
        }

        public string Text { get; }

        public SurfaceOwner Owner { get; }
    }

    /// <summary>
    /// The surface of one reference assembly written out as lines, in a fixed order, each with the
    /// type and member it belongs to. Two assemblies are the same surface exactly when their texts are
    /// equal line by line; the owners only say what a difference names.
    /// </summary>
    internal sealed class SurfaceDescription
    {
        private readonly List<string> texts = new List<string>();
        private readonly List<SurfaceEntry> entries = new List<SurfaceEntry>();

        public IReadOnlyList<string> Texts => texts;

        public IReadOnlyList<SurfaceEntry> Entries => entries;

        public void Add(string text, SurfaceOwner owner)
        {
            texts.Add(text);
            entries.Add(new SurfaceEntry(text, owner));
        }

        public void AddPrefixed(string prefix, List<string> lines, SurfaceOwner owner)
        {
            foreach (string line in lines)
            {
                Add(prefix + line, owner);
            }
        }
    }
}
