using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.Tests
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
            // Note: 実際のログ取得結果は環境に依存するため、レスポンス構造のみ検証
        }

        /// <summary>
        /// 空のログ取得テスト
        /// </summary>
        [Test]
        public async Task ExecuteAsync_EmptyLogType_HandlesGracefully()
        {
            // Arrange
            var useCase = new GetLogsUseCase();
            var schema = new GetLogsSchema
            {
                LogType = McpLogType.None,
                MaxCount = 0,
                TimeoutSeconds = 10
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            // 空のパラメータでも適切に処理されることを確認
        }
    }
}