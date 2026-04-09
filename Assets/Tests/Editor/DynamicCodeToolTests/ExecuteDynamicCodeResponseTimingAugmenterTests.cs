using System.Collections.Generic;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class ExecuteDynamicCodeResponseTimingAugmenterTests
    {
        [Test]
        public void AppendTimingEntries_WhenResponseHasExistingTimings_ShouldAppendRpcTimings()
        {
            ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
            {
                Timings = new List<string>
                {
                    "[Perf] Backend: SharedRoslynWorker"
                }
            };

            ExecuteDynamicCodeResponseTimingAugmenter.AppendTimingEntries(
                response,
                12.3,
                45.6,
                78.9);

            Assert.That(response.Timings, Has.Member("[Perf] Backend: SharedRoslynWorker"));
            Assert.That(response.Timings, Has.Member("[Perf] MainThreadWait: 12.3ms"));
            Assert.That(response.Timings, Has.Member("[Perf] ToolTotal: 45.6ms"));
            Assert.That(response.Timings, Has.Member("[Perf] RequestTotal: 78.9ms"));
        }

        [Test]
        public void AppendTimingEntries_WhenResponseTimingsAreNull_ShouldCreateTimingList()
        {
            ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
            {
                Timings = null
            };

            ExecuteDynamicCodeResponseTimingAugmenter.AppendTimingEntries(
                response,
                1.0,
                2.0,
                3.0);

            Assert.That(response.Timings, Is.Not.Null);
            Assert.That(response.Timings, Has.Count.EqualTo(3));
        }
    }
}
