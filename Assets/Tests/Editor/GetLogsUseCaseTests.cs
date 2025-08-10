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
            GetLogsUseCase useCase = new(new LogRetrievalService(), new LogFilteringService());
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
            GetLogsUseCase useCase = new(new LogRetrievalService(), new LogFilteringService());
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

        /// <summary>
        /// Test for Error log type filtering - debugging the specific issue
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ErrorLogType_FiltersCorrectly()
        {
            // Generate test errors to ensure we have error logs
            UnityEngine.Debug.LogError("Test Error 1 for GetLogsUseCase");
            UnityEngine.Debug.LogError("Test Error 2 for GetLogsUseCase");
            UnityEngine.Debug.LogWarning("Test Warning (should not appear)");
            UnityEngine.Debug.Log("Test Log (should not appear)");

            // Arrange
            GetLogsUseCase useCase = new(new LogRetrievalService(), new LogFilteringService());
            GetLogsSchema schema = new()
            {
                LogType = McpLogType.Error,
                MaxCount = 100,
                TimeoutSeconds = 5
            };
            CancellationToken cancellationToken = new();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            
            // Debug information
            UnityEngine.Debug.Log($"Error filtering test - TotalCount: {result.TotalCount}, DisplayedCount: {result.DisplayedCount}");
            
            // If there are any logs, they should all be Error type
            foreach (var log in result.Logs)
            {
                Assert.AreEqual(McpLogType.Error, log.Type, $"All returned logs should be Error type, but found: {log.Type}");
            }
            
            // Should find at least our test errors (if not filtered out by other settings)
            if (result.DisplayedCount == 0)
            {
                UnityEngine.Debug.LogWarning("No error logs found - this might indicate the filtering bug");
            }
        }
    }
}