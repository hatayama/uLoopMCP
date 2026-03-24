using System.Collections.Generic;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DangerousApiCatalogTests
    {
        [Test]
        public void EnumerateDangerousMembers_ShouldReturnDefensiveCopies()
        {
            List<KeyValuePair<string, IReadOnlyCollection<string>>> snapshot =
                new List<KeyValuePair<string, IReadOnlyCollection<string>>>(DangerousApiCatalog.EnumerateDangerousMembers());

            int processEntryIndex = snapshot.FindIndex(entry => entry.Key == "System.Diagnostics.Process");
            Assert.AreNotEqual(-1, processEntryIndex, "Process entry should exist in the catalog");

            KeyValuePair<string, IReadOnlyCollection<string>> processEntry = snapshot[processEntryIndex];
            List<string> mutableCopy = processEntry.Value as List<string>;
            Assert.IsNotNull(mutableCopy, "Returned collection should be a defensive copy");

            mutableCopy.Remove("Start");

            Assert.IsTrue(
                DangerousApiCatalog.IsDangerousApi("System.Diagnostics.Process", "Start"),
                "Mutating an enumerated copy must not weaken the catalog");
        }
    }
}
