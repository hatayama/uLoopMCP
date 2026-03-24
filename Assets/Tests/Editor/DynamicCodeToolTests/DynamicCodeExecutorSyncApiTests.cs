using NUnit.Framework;
using System.Threading;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeExecutorSyncApiTests
    {
        [Test]
        public void ExecuteCode_WhenCalledSynchronously_ShouldThrowNotSupportedException()
        {
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted);

            Assert.Throws<System.NotSupportedException>(() =>
                executor.ExecuteCode(
                    "return 1 + 2;",
                    "DynamicCommand",
                    null,
                    CancellationToken.None,
                    false));
        }
    }
}
