using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    /// <summary>
    /// PlayModeTestExecuterのフィルター処理テスト
    /// regexフィルターが正しく動作することを確認
    /// </summary>
    public class PlayModeTestExecuterTests
    {
        /// <summary>
        /// regexフィルターでUnity Filterが正しく生成されることをテスト
        /// </summary>
        [Test]
        public void CreateUnityFilter_WithRegexFilter_ShouldSetGroupNamesCorrectly()
        {
            // Arrange
            TestExecutionFilter filter = new(TestExecutionFilterType.Regex, "ExecuteDynamicCodeUsingStatementTests");
            
            // Act - reflection経由でprivateメソッドを呼び出し
            System.Reflection.MethodInfo createUnityFilterMethod = typeof(PlayModeTestExecuter)
                .GetMethod("CreateUnityFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Filter unityFilter = (Filter)createUnityFilterMethod.Invoke(null, new object[] { TestMode.EditMode, filter });
            
            // Assert
            Assert.That(unityFilter.testMode, Is.EqualTo(TestMode.EditMode));
            Assert.That(unityFilter.groupNames, Is.Not.Null);
            Assert.That(unityFilter.groupNames.Length, Is.EqualTo(1));
            Assert.That(unityFilter.groupNames[0], Is.EqualTo("ExecuteDynamicCodeUsingStatementTests"));
            
            // exactフィルター用のtestNamesは設定されていない
            Assert.That(unityFilter.testNames, Is.Null);
            // assemblyフィルター用のassemblyNamesは設定されていない  
            Assert.That(unityFilter.assemblyNames, Is.Null);
        }
        
        /// <summary>
        /// exactフィルターでUnity Filterが正しく生成されることをテスト
        /// </summary>
        [Test]
        public void CreateUnityFilter_WithExactFilter_ShouldSetTestNamesCorrectly()
        {
            // Arrange
            TestExecutionFilter filter = new(TestExecutionFilterType.Exact, "io.github.hatayama.uLoopMCP.Tests.Editor.ExecuteDynamicCodeUsingStatementTests.TestAssetDatabaseUsage_ShouldAutoAddUnityEditorUsing");
            
            // Act
            System.Reflection.MethodInfo createUnityFilterMethod = typeof(PlayModeTestExecuter)
                .GetMethod("CreateUnityFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Filter unityFilter = (Filter)createUnityFilterMethod.Invoke(null, new object[] { TestMode.EditMode, filter });
            
            // Assert
            Assert.That(unityFilter.testMode, Is.EqualTo(TestMode.EditMode));
            Assert.That(unityFilter.testNames, Is.Not.Null);
            Assert.That(unityFilter.testNames.Length, Is.EqualTo(1));
            Assert.That(unityFilter.testNames[0], Is.EqualTo("io.github.hatayama.uLoopMCP.Tests.Editor.ExecuteDynamicCodeUsingStatementTests.TestAssetDatabaseUsage_ShouldAutoAddUnityEditorUsing"));
            
            // regexフィルター用のgroupNamesは設定されていない
            Assert.That(unityFilter.groupNames, Is.Null);
            // assemblyフィルター用のassemblyNamesは設定されていない
            Assert.That(unityFilter.assemblyNames, Is.Null);
        }
        
        /// <summary>
        /// assemblyフィルターでUnity Filterが正しく生成されることをテスト
        /// </summary>
        [Test]
        public void CreateUnityFilter_WithAssemblyFilter_ShouldSetAssemblyNamesCorrectly()
        {
            // Arrange
            TestExecutionFilter filter = new(TestExecutionFilterType.AssemblyName, "uLoopMCP.Tests.Editor");
            
            // Act
            System.Reflection.MethodInfo createUnityFilterMethod = typeof(PlayModeTestExecuter)
                .GetMethod("CreateUnityFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Filter unityFilter = (Filter)createUnityFilterMethod.Invoke(null, new object[] { TestMode.PlayMode, filter });
            
            // Assert
            Assert.That(unityFilter.testMode, Is.EqualTo(TestMode.PlayMode));
            Assert.That(unityFilter.assemblyNames, Is.Not.Null);
            Assert.That(unityFilter.assemblyNames.Length, Is.EqualTo(1));
            Assert.That(unityFilter.assemblyNames[0], Is.EqualTo("uLoopMCP.Tests.Editor"));
            
            // 他のフィルターは設定されていない
            Assert.That(unityFilter.testNames, Is.Null);
            Assert.That(unityFilter.groupNames, Is.Null);
        }
        
        /// <summary>
        /// フィルターがnullの場合、全テスト対象のFilterが生成されることをテスト
        /// </summary>
        [Test]
        public void CreateUnityFilter_WithNullFilter_ShouldCreateAllTestsFilter()
        {
            // Act
            System.Reflection.MethodInfo createUnityFilterMethod = typeof(PlayModeTestExecuter)
                .GetMethod("CreateUnityFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Filter unityFilter = (Filter)createUnityFilterMethod.Invoke(null, new object[] { TestMode.EditMode, null });
            
            // Assert
            Assert.That(unityFilter.testMode, Is.EqualTo(TestMode.EditMode));
            Assert.That(unityFilter.testNames, Is.Null);
            Assert.That(unityFilter.groupNames, Is.Null);
            Assert.That(unityFilter.assemblyNames, Is.Null);
        }
        
        /// <summary>
        /// Allフィルタータイプでもフィルターなしと同じ動作になることをテスト
        /// </summary>
        [Test]
        public void CreateUnityFilter_WithAllFilterType_ShouldCreateAllTestsFilter()
        {
            // Arrange
            TestExecutionFilter filter = new(TestExecutionFilterType.All, "ignored");
            
            // Act
            System.Reflection.MethodInfo createUnityFilterMethod = typeof(PlayModeTestExecuter)
                .GetMethod("CreateUnityFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Filter unityFilter = (Filter)createUnityFilterMethod.Invoke(null, new object[] { TestMode.EditMode, filter });
            
            // Assert
            Assert.That(unityFilter.testMode, Is.EqualTo(TestMode.EditMode));
            Assert.That(unityFilter.testNames, Is.Null);
            Assert.That(unityFilter.groupNames, Is.Null);
            Assert.That(unityFilter.assemblyNames, Is.Null);
        }
        
        /// <summary>
        /// 修正前のregexフィルターの問題を確認するテスト
        /// 修正前はRegex.Escapeを使ってたため、パターンとして機能しなかった
        /// </summary>
        [Test]
        public void CreateUnityFilter_RegexFilter_ShouldNotEscapeFilterValue()
        {
            // Arrange - 特殊文字を含むregexパターン
            TestExecutionFilter filter = new(TestExecutionFilterType.Regex, ".*Tests$");  // 実際のregexパターン
            
            // Act
            System.Reflection.MethodInfo createUnityFilterMethod = typeof(PlayModeTestExecuter)
                .GetMethod("CreateUnityFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Filter unityFilter = (Filter)createUnityFilterMethod.Invoke(null, new object[] { TestMode.EditMode, filter });
            
            // Assert - パターンがそのまま設定されている（エスケープされていない）
            Assert.That(unityFilter.groupNames[0], Is.EqualTo(".*Tests$"));
            
            // 修正前だとこうなってた（間違い）: "^.*Tests\\$$"
            Assert.That(unityFilter.groupNames[0], Is.Not.EqualTo("^.*Tests\\$$"));
        }
    }
}