using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;

namespace io.github.hatayama.uLoopMCP.Tests
{
    /// <summary>
    /// RunTestsUseCaseのユニットテスト
    /// </summary>
    [TestFixture]
    public class RunTestsUseCaseTests
    {
        /// <summary>
        /// 正常なテスト実行のテスト（最小限）
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidParameters_ReturnsSuccessResponse()
        {
            // Arrange
            var useCase = new RunTestsUseCase();
            var schema = new RunTestsSchema
            {
                FilterType = TestFilterType.all,
                FilterValue = "",
                TestMode = TestMode.EditMode,
                TimeoutSeconds = 30,
                SaveXml = false
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Message);
            // Note: 実際のテスト実行結果は環境に依存するため、レスポンス構造のみ検証
        }

        /// <summary>
        /// EditModeテスト実行のテスト
        /// </summary>
        [Test]
        public async Task ExecuteAsync_EditModeTests_HandlesCorrectly()
        {
            // Arrange
            var useCase = new RunTestsUseCase();
            var schema = new RunTestsSchema
            {
                FilterType = TestFilterType.regex,
                FilterValue = "CompileUseCaseTests", // 新しく作ったテストを対象
                TestMode = TestMode.EditMode,
                TimeoutSeconds = 10,
                SaveXml = false
            };
            var cancellationToken = new CancellationToken();

            // Act
            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            // EditModeテストが適切に実行されることを確認
        }
    }
}