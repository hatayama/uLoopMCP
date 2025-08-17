using NUnit.Framework;
using System;

namespace io.github.hatayama.uLoopMCP
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
            // テスト開始時にデフォルトレベルに戻す
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
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

        [Test]
        public void CurrentLevelプロパティの取得が動作するか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
            
            // Act
            DynamicCodeSecurityLevel level = DynamicCodeSecurityManager.CurrentLevel;
            
            // Assert
            Assert.AreEqual(DynamicCodeSecurityLevel.FullAccess, level);
        }

        [Test]
        public void CurrentLevelプロパティの設定が動作するか確認()
        {
            // Act
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Disabled);
            
            // Assert
            Assert.AreEqual(DynamicCodeSecurityLevel.Disabled, DynamicCodeSecurityManager.CurrentLevel);
        }

        [Test]
        public void SecurityLevelChangedイベントが発火するか確認()
        {
            // Arrange
            bool eventFired = false;
            DynamicCodeSecurityLevel capturedLevel = DynamicCodeSecurityLevel.Restricted;
            
            Action<DynamicCodeSecurityLevel> handler = (level) =>
            {
                eventFired = true;
                capturedLevel = level;
            };
            
            DynamicCodeSecurityManager.SecurityLevelChanged += handler;
            
            try
            {
                // Act
                SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
                
                // Assert
                Assert.IsTrue(eventFired);
                Assert.AreEqual(DynamicCodeSecurityLevel.FullAccess, capturedLevel);
            }
            finally
            {
                DynamicCodeSecurityManager.SecurityLevelChanged -= handler;
            }
        }

        [Test]
        public void SecurityLevelChangedイベントが同じレベル設定では発火しないか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            bool eventFired = false;
            
            Action<DynamicCodeSecurityLevel> handler = (level) =>
            {
                eventFired = true;
            };
            
            DynamicCodeSecurityManager.SecurityLevelChanged += handler;
            
            try
            {
                // Act
                SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
                
                // Assert
                Assert.IsFalse(eventFired);
            }
            finally
            {
                DynamicCodeSecurityManager.SecurityLevelChanged -= handler;
            }
        }

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
            Assert.Greater(level2Assemblies.Count, level1Assemblies.Count);
        }
    }
}