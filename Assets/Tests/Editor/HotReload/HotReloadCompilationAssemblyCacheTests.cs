using System;
using System.Collections.Generic;

using NUnit.Framework;

using UnityEditor.Compilation;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// EditMode coverage for the per-domain memo of the compilation assembly list: when it asks Unity,
    /// when it reuses the last answer, and why an empty answer is never kept.
    /// </summary>
    public sealed class HotReloadCompilationAssemblyCacheTests
    {
        private int _fetchCount;
        private Queue<UnityCompilationAssembly[]> _answers;

        [SetUp]
        public void SetUp()
        {
            _fetchCount = 0;
            _answers = new Queue<UnityCompilationAssembly[]>();
        }

        /// <summary>
        /// Verifies that a second Current() reuses the first answer without asking Unity again.
        /// </summary>
        [Test]
        public void Current_CalledTwice_FetchesOnceAndReturnsSameInstance()
        {
            _answers.Enqueue(new[] { CreateAssembly("First") });
            HotReloadCompilationAssemblyCache cache = CreateCache();

            IReadOnlyList<UnityCompilationAssembly> first = cache.Current();
            IReadOnlyList<UnityCompilationAssembly> second = cache.Current();

            Assert.That(_fetchCount, Is.EqualTo(1));
            Assert.That(second, Is.SameAs(first));
            Assert.That(cache.IsFilled, Is.True);
        }

        /// <summary>
        /// Verifies that Invalidate() drops the memo so the next Current() asks Unity for a new list.
        /// </summary>
        [Test]
        public void Current_AfterInvalidate_FetchesAgainAndReturnsNewAnswer()
        {
            UnityCompilationAssembly[] firstAnswer = { CreateAssembly("First") };
            UnityCompilationAssembly[] secondAnswer = { CreateAssembly("Second") };
            _answers.Enqueue(firstAnswer);
            _answers.Enqueue(secondAnswer);
            HotReloadCompilationAssemblyCache cache = CreateCache();
            cache.Current();

            cache.Invalidate();
            IReadOnlyList<UnityCompilationAssembly> afterInvalidate = cache.Current();

            Assert.That(_fetchCount, Is.EqualTo(2));
            Assert.That(afterInvalidate, Is.SameAs(secondAnswer));
        }

        /// <summary>
        /// Verifies that an empty answer, which Unity gives while a compile is in flight, is not kept.
        /// </summary>
        [Test]
        public void Current_EmptyAnswer_IsNotMemoized()
        {
            _answers.Enqueue(Array.Empty<UnityCompilationAssembly>());
            _answers.Enqueue(Array.Empty<UnityCompilationAssembly>());
            HotReloadCompilationAssemblyCache cache = CreateCache();

            IReadOnlyList<UnityCompilationAssembly> first = cache.Current();
            bool filledAfterEmpty = cache.IsFilled;
            cache.Current();

            Assert.That(first, Is.Empty);
            Assert.That(filledAfterEmpty, Is.False);
            Assert.That(_fetchCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Verifies that the first non-empty answer after an empty one is kept.
        /// </summary>
        [Test]
        public void Current_NonEmptyAfterEmpty_IsMemoized()
        {
            UnityCompilationAssembly[] nonEmpty = { CreateAssembly("First") };
            _answers.Enqueue(Array.Empty<UnityCompilationAssembly>());
            _answers.Enqueue(nonEmpty);
            HotReloadCompilationAssemblyCache cache = CreateCache();

            cache.Current();
            cache.Current();
            IReadOnlyList<UnityCompilationAssembly> third = cache.Current();

            Assert.That(_fetchCount, Is.EqualTo(2));
            Assert.That(third, Is.SameAs(nonEmpty));
        }

        /// <summary>
        /// Verifies that FindByName returns the assembly with that name and asks Unity only once across lookups.
        /// </summary>
        [Test]
        public void FindByName_KnownName_ReturnsAssemblyAndFetchesOnce()
        {
            UnityCompilationAssembly target = CreateAssembly("Second");
            _answers.Enqueue(new[] { CreateAssembly("First"), target });
            HotReloadCompilationAssemblyCache cache = CreateCache();

            UnityCompilationAssembly first = cache.FindByName("Second");
            UnityCompilationAssembly second = cache.FindByName("Second");

            Assert.That(first, Is.SameAs(target));
            Assert.That(second, Is.SameAs(target));
            Assert.That(_fetchCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies that FindByName matches the whole name with case, not a prefix or a case-folded name,
        /// and that it looks at the first assembly too.
        /// </summary>
        [Test]
        public void FindByName_SimilarNames_ReturnsExactMatchOnly()
        {
            UnityCompilationAssembly lowerCase = CreateAssembly("foo");
            UnityCompilationAssembly prefixed = CreateAssembly("Foo.Editor");
            UnityCompilationAssembly exact = CreateAssembly("Foo");
            _answers.Enqueue(new[] { lowerCase, prefixed, exact });
            HotReloadCompilationAssemblyCache cache = CreateCache();

            UnityCompilationAssembly foundUpper = cache.FindByName("Foo");
            UnityCompilationAssembly foundLower = cache.FindByName("foo");

            Assert.That(foundUpper, Is.SameAs(exact));
            Assert.That(foundLower, Is.SameAs(lowerCase));
        }

        /// <summary>
        /// Verifies that FindByName returns null for a name no compilation assembly has.
        /// </summary>
        [Test]
        public void FindByName_UnknownName_ReturnsNull()
        {
            _answers.Enqueue(new[] { CreateAssembly("First") });
            HotReloadCompilationAssemblyCache cache = CreateCache();

            UnityCompilationAssembly found = cache.FindByName("Missing");

            Assert.That(found, Is.Null);
        }

        /// <summary>
        /// Verifies that a list built off the main thread is kept, without asking Unity, when nothing
        /// is kept and no invalidation happened since its generation was read.
        /// </summary>
        [Test]
        public void TryFill_WhenEmptyAtTheSameGeneration_KeepsTheRebuiltList()
        {
            UnityCompilationAssembly[] rebuilt = { CreateAssembly("Rebuilt") };
            HotReloadCompilationAssemblyCache cache = CreateCache();

            bool filled = cache.TryFill(rebuilt, cache.Generation);

            Assert.That(filled, Is.True);
            Assert.That(cache.Current(), Is.SameAs(rebuilt));
            Assert.That(_fetchCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies that a list Unity already answered wins over a rebuilt one.
        /// </summary>
        [Test]
        public void TryFill_WhenAlreadyFilled_KeepsTheFetchedList()
        {
            UnityCompilationAssembly[] fetched = { CreateAssembly("Fetched") };
            _answers.Enqueue(fetched);
            HotReloadCompilationAssemblyCache cache = CreateCache();
            cache.Current();

            bool filled = cache.TryFill(new[] { CreateAssembly("Rebuilt") }, cache.Generation);

            Assert.That(filled, Is.False);
            Assert.That(cache.Current(), Is.SameAs(fetched));
        }

        /// <summary>
        /// Verifies that a list built before an invalidation is refused even though nothing is kept,
        /// because the script set may have changed while it was built.
        /// </summary>
        [Test]
        public void TryFill_AfterAnInvalidation_Refuses()
        {
            HotReloadCompilationAssemblyCache cache = CreateCache();
            int generationBeforeBuild = cache.Generation;
            cache.Invalidate();

            bool filled = cache.TryFill(new[] { CreateAssembly("Rebuilt") }, generationBeforeBuild);

            Assert.That(filled, Is.False);
            Assert.That(cache.IsFilled, Is.False);
        }

        private HotReloadCompilationAssemblyCache CreateCache()
        {
            return new HotReloadCompilationAssemblyCache(Fetch);
        }

        private UnityCompilationAssembly[] Fetch()
        {
            _fetchCount++;
            return _answers.Dequeue();
        }

        private static UnityCompilationAssembly CreateAssembly(string assemblyName)
        {
            return new UnityCompilationAssembly(
                assemblyName,
                HotReloadConstants.ScriptAssembliesRelativeDirectory + "/" + assemblyName + HotReloadConstants.CompiledAssemblyExtension,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<UnityCompilationAssembly>(),
                Array.Empty<string>(),
                AssemblyFlags.EditorAssembly);
        }
    }
}
