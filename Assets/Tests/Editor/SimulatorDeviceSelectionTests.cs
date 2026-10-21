using System.Collections.Generic;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies how a device name given on the command line is matched against the Device
    /// Simulator's device list.
    /// </summary>
    public sealed class SimulatorDeviceSelectionTests
    {
        private static readonly string[] IPhoneFamily =
        {
            "Apple iPhone 12",
            "Apple iPhone 12 Pro",
            "Apple iPhone 12 Pro Max",
            "Apple iPhone 12 mini",
            "Apple iPhone SE (2nd generation)",
            "Google Pixel 5",
        };

        private static SimulatorDeviceSelectionResult Resolve(string query, IReadOnlyList<string> names)
        {
            return SimulatorDeviceSelection.Resolve(query, names);
        }

        [Test]
        public void Resolve_ExactNameIgnoringCase_WinsOverLongerNames()
        {
            // Verifies a name that matches whole, in any casing, beats names that merely contain it.
            SimulatorDeviceSelectionResult result = Resolve("apple iphone 12", IPhoneFamily);

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Selected));
            Assert.That(result.DeviceName, Is.EqualTo("Apple iPhone 12"));
        }

        [Test]
        public void Resolve_DuplicateExactNames_IsAmbiguous()
        {
            // Verifies two identical names cannot be told apart and are both reported.
            SimulatorDeviceSelectionResult result = Resolve("Foo", new[] { "Foo", "Foo" });

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Ambiguous));
            Assert.That(result.Candidates, Is.EqualTo(new[] { "Foo", "Foo" }));
        }

        [Test]
        public void Resolve_WordBoundarySuffix_PicksTheBaseModel()
        {
            // Verifies "iPhone 12" selects the base model although the Pro, Pro Max, and mini
            // variants also contain those words.
            SimulatorDeviceSelectionResult result = Resolve("iPhone 12", IPhoneFamily);

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Selected));
            Assert.That(result.DeviceName, Is.EqualTo("Apple iPhone 12"));
        }

        [Test]
        public void Resolve_SuffixNeedsAWordBoundary()
        {
            // Verifies "5" end-matches "Google Pixel 5" only: without the word boundary it would
            // also end-match "Google Pixel 15" and be ambiguous.
            SimulatorDeviceSelectionResult result = Resolve("5", new[] { "Google Pixel 15", "Google Pixel 5" });

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Selected));
            Assert.That(result.DeviceName, Is.EqualTo("Google Pixel 5"));
        }

        [Test]
        public void Resolve_SeveralSuffixMatches_ListsThemInOrder()
        {
            // Verifies several end-matches are reported as candidates in list order, and that a name
            // merely containing the words (Fold 2) is not among them.
            SimulatorDeviceSelectionResult result =
                Resolve("Fold", new[] { "Samsung Galaxy Fold", "Samsung Galaxy Fold 2", "Huawei Mate Fold", "Google Pixel 5" });

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Ambiguous));
            Assert.That(result.Candidates, Is.EqualTo(new[] { "Samsung Galaxy Fold", "Huawei Mate Fold" }));
        }

        [Test]
        public void Resolve_UniqueSubstring_IsSelected()
        {
            // Verifies a fragment found inside exactly one name selects that name.
            SimulatorDeviceSelectionResult result = Resolve("SE (2nd", IPhoneFamily);

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Selected));
            Assert.That(result.DeviceName, Is.EqualTo("Apple iPhone SE (2nd generation)"));
        }

        [Test]
        public void Resolve_SeveralSubstringMatches_ListsThemInOrder()
        {
            // Verifies a fragment found inside several names, none of which ends with it, is ambiguous
            // and lists only those names in list order.
            SimulatorDeviceSelectionResult result = Resolve("iPhone", IPhoneFamily);

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Ambiguous));
            Assert.That(result.Candidates, Is.EqualTo(new[]
            {
                "Apple iPhone 12",
                "Apple iPhone 12 Pro",
                "Apple iPhone 12 Pro Max",
                "Apple iPhone 12 mini",
                "Apple iPhone SE (2nd generation)",
            }));
        }

        [Test]
        public void Resolve_NoMatch_IsNotFound()
        {
            // Verifies a name no device contains is reported as not found with no candidates.
            SimulatorDeviceSelectionResult result = Resolve("Nokia", IPhoneFamily);

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.NotFound));
            Assert.That(result.DeviceName, Is.Null);
            Assert.That(result.Candidates, Is.Empty);
        }

        [Test]
        public void Resolve_TrimsSurroundingWhitespace()
        {
            // Verifies stray spaces around the name do not prevent a match.
            SimulatorDeviceSelectionResult result = Resolve("  iPhone 12 ", IPhoneFamily);

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.Selected));
            Assert.That(result.DeviceName, Is.EqualTo("Apple iPhone 12"));
        }

        [Test]
        public void Resolve_EmptyList_IsNotFound()
        {
            // Verifies an Editor with no devices yields not found rather than an exception.
            SimulatorDeviceSelectionResult result = Resolve("iPhone 12", new string[0]);

            Assert.That(result.Kind, Is.EqualTo(SimulatorDeviceMatchKind.NotFound));
        }
    }
}
