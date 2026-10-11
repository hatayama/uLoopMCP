using System.Collections.Generic;

using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Pins the order of the production warm-up items.
    /// </summary>
    public sealed class HotReloadWarmUpItemsTests
    {
        /// <summary>
        /// What: the items run costliest first, and writing the publicized copy costs the most. The
        /// transform worker starts after the referenced-method sets, and the PDB documents come last
        /// because their lookup can throw an exception that stops the items after it.
        /// </summary>
        [Test]
        public void CreateProduction_ReturnsPublicizedTargetsThenCallSitesThenReferencedMethodSetsThenTransformWorkerThenPdbDocuments()
        {
            List<string> names = new List<string>();
            foreach (IHotReloadWarmUpItem item in HotReloadWarmUpItems.CreateProduction(HotReloadCompiledCallers.CreateProduction()))
            {
                names.Add(item.Name);
            }

            Assert.That(
                names,
                Is.EqualTo(new[] { "publicized_targets", "call_sites", "referenced_method_sets", "transform_worker", "pdb_documents" }));
        }

        /// <summary>
        /// What: exactly the items that read the compilation assembly list say they need it: the
        /// publicized copies and the transform worker look their target up in it, and the
        /// referenced-method sets read the referencing dlls only it names. These are the items
        /// skipped when no list was kept.
        /// </summary>
        [Test]
        public void CreateProduction_MarksTheItemsThatReadTheCompilationList()
        {
            List<string> needing = new List<string>();
            foreach (IHotReloadWarmUpItem item in HotReloadWarmUpItems.CreateProduction(HotReloadCompiledCallers.CreateProduction()))
            {
                if (item.NeedsCompilationList)
                {
                    needing.Add(item.Name);
                }
            }

            Assert.That(needing, Is.EqualTo(new[] { "publicized_targets", "referenced_method_sets", "transform_worker" }));
        }
    }
}
