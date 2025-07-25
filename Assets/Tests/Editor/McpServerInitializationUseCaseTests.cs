using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.Tests
{
    /// <summary>
    /// McpServerInitializationUseCaseのユニットテスト
    /// </summary>
    [TestFixture]
    public class McpServerInitializationUseCaseTests
    {
        /// <summary>
        /// 正常なサーバー初期化のテスト（最小限）
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidPort_ReturnsResponse()
        {
            // Arrange
            var useCase = new McpServerInitializationUseCase();
            var schema = new ServerInitializationSchema
            {
                Port = 8888 // テスト用ポート
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Message);
            Assert.IsTrue(result.ServerPort > 0);
            // Note: 実際の初期化成功/失敗は環境に依存するため、レスポンス構造のみ検証
        }

        /// <summary>
        /// デフォルトポートでの初期化テスト
        /// </summary>
        [Test]
        public async Task ExecuteAsync_DefaultPort_HandlesCorrectly()
        {
            // Arrange
            var useCase = new McpServerInitializationUseCase();
            var schema = new ServerInitializationSchema
            {
                Port = -1 // デフォルトポート指定
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            // デフォルトポート指定でも適切に処理されることを確認
        }
    }
}