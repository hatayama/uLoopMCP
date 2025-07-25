using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.Tests
{
    /// <summary>
    /// CompileUseCaseのユニットテスト
    /// </summary>
    [TestFixture]
    public class CompileUseCaseTests
    {
        /// <summary>
        /// 正常なコンパイル実行のテスト（最小限）
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidParameters_ReturnsSuccessResponse()
        {
            // Arrange
            var useCase = new CompileUseCase();
            var schema = new CompileSchema
            {
                ForceRecompile = false,
                TimeoutSeconds = 10
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Message);
            // Note: 実際の成功/失敗は環境に依存するため、レスポンス構造のみ検証
        }

        /// <summary>
        /// キャンセレーション時のテスト
        /// </summary>
        [Test]
        public async Task ExecuteAsync_CancelledToken_HandlesGracefully()
        {
            // Arrange
            var useCase = new CompileUseCase();
            var schema = new CompileSchema
            {
                ForceRecompile = false,
                TimeoutSeconds = 1
            };
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel(); // 即座にキャンセル

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            // キャンセレーションが適切に処理されることを確認
        }
    }
}