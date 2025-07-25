using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unit tests for GetLogsUseCase
    /// Related classes: GetLogsUseCase, LogRetrievalService, LogFilteringService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    [TestFixture]
    public class GetLogsUseCaseTests
    {
        /// <summary>
        /// Test for normal log retrieval execution (minimal)
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidParameters_ReturnsSuccessResponse()
        {
            // Arrange
            GetLogsUseCase useCase = new();
            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = 10,
                TimeoutSeconds = 10
            };
            CancellationToken cancellationToken = new();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.IsTrue(result.TotalCount >= 0);
            Assert.IsTrue(result.DisplayedCount >= 0);
            // Note: Actual log retrieval results depend on environment, so only response structure is verified
        }

        /// <summary>
        /// Test for small log count retrieval
        /// </summary>
        [Test]
        public async Task ExecuteAsync_SmallMaxCount_HandlesCorrectly()
        {
            // Arrange
            GetLogsUseCase useCase = new();
            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = 1,
                TimeoutSeconds = 5
            };
            CancellationToken cancellationToken = new();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.IsTrue(result.DisplayedCount <= 1);
            // Verify that small MaxCount is handled properly
        }
    }
}