using NUnit.Framework;
using System;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// DynamicCodeSecurityManagerのテスト
    /// セキュリティレベル管理と危険API検出機能を確認
    /// </summary>
    [TestFixture]
    public class DynamicCodeSecurityManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            // v4.0ステートレス設計のためSetUp不要
        }

        [Test]
        public void CanExecute_Level0でfalseを返すか確認()
        {
            // Act
            bool canExecute = DynamicCodeSecurityManager.CanExecute(DynamicCodeSecurityLevel.Disabled);
            
            // Assert
            Assert.IsFalse(canExecute);
        }

        [Test]
        public void CanExecute_Level1でtrueを返すか確認()
        {
            // Act
            bool canExecute = DynamicCodeSecurityManager.CanExecute(DynamicCodeSecurityLevel.Restricted);
            
            // Assert
            Assert.IsTrue(canExecute);
        }

        [Test]
        public void CanExecute_Level2でtrueを返すか確認()
        {
            // Act
            bool canExecute = DynamicCodeSecurityManager.CanExecute(DynamicCodeSecurityLevel.FullAccess);
            
            // Assert
            Assert.IsTrue(canExecute);
        }

        // ContainsDangerousApiメソッドのテストは削除（RoslynベースのSecurityValidatorに移行）
        // 新しいテストはRestrictedModeUserClassTestsに実装済み

        // CurrentLevelプロパティのテスト削除（v4.0では常にDisabledを返すため）

        // SecurityLevelChangedイベントのテスト削除（v4.0ではイベント発火しないため）

        // IsCodeAllowedForCurrentLevelメソッドは削除（RoslynベースのSecurityValidatorに移行）
        // 関連テストは廃止

        [Test]
        public void GetAllowedAssemblies_各レベルで適切なリストを返すか確認()
        {
            // Act
            var level0Assemblies = DynamicCodeSecurityManager.GetAllowedAssemblies(DynamicCodeSecurityLevel.Disabled);
            var level1Assemblies = DynamicCodeSecurityManager.GetAllowedAssemblies(DynamicCodeSecurityLevel.Restricted);
            var level2Assemblies = DynamicCodeSecurityManager.GetAllowedAssemblies(DynamicCodeSecurityLevel.FullAccess);
            
            // Assert
            Assert.AreEqual(0, level0Assemblies.Count);
            Assert.Greater(level1Assemblies.Count, 0);
            // 新仕様: RestrictedとFullAccessは同じアセンブリ数を返す
            Assert.AreEqual(level2Assemblies.Count, level1Assemblies.Count);
        }
    }
}