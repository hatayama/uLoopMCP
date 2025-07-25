using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GetLogsUseCaseのユニットテスト
    /// </summary>
    [TestFixture]
    public class GetLogsUseCaseTests
    {
        /// <summary>
        /// 正常なログ取得実行のテスト（最小限）
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidParameters_ReturnsSuccessResponse()
        {
            // Arrange
            var useCase = new GetLogsUseCase();
            var schema = new GetLogsSchema
            {
                LogType = McpLogType.All,
                MaxCount = 10,
                TimeoutSeconds = 10
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.IsTrue(result.TotalCount >= 0);
            Assert.IsTrue(result.DisplayedCount >= 0);
            // Note: 実際のログ取得結果は環境に依存するため、レスポンス構造のみ検証
        }

        /// <summary>
        /// 少ないログ取得テスト
        /// </summary>
        [Test]
        public async Task ExecuteAsync_SmallMaxCount_HandlesCorrectly()
        {
            // Arrange
            var useCase = new GetLogsUseCase();
            var schema = new GetLogsSchema
            {
                LogType = McpLogType.All,
                MaxCount = 1,
                TimeoutSeconds = 5
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.IsTrue(result.DisplayedCount <= 1);
            // 少ないMaxCountでも適切に処理されることを確認
        }
    }
}