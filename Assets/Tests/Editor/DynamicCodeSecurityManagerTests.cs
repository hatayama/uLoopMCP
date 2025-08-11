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

        [Test]
        public void ContainsDangerousApi_SystemIOFileを検出するか確認()
        {
            // Arrange
            string code = "System.IO.File.ReadAllText(\"test.txt\");";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsTrue(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_Fileクラスを検出するか確認()
        {
            // Arrange
            string code = "File.WriteAllText(\"test.txt\", \"content\");";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsTrue(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_HttpClientを検出するか確認()
        {
            // Arrange
            string code = "var client = new HttpClient();";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsTrue(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_SystemNetを検出するか確認()
        {
            // Arrange
            string code = "System.Net.WebRequest.Create(\"http://example.com\");";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsTrue(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_Processを検出するか確認()
        {
            // Arrange
            string code = "Process.Start(\"notepad.exe\");";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsTrue(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_リフレクションを検出するか確認()
        {
            // Arrange
            string code = "Type.GetType(\"SomeClass\").GetMethod(\"SomeMethod\");";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsTrue(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_GameObjectCreatePrimitiveは検出しないか確認()
        {
            // Arrange
            string code = "GameObject.CreatePrimitive(PrimitiveType.Cube);";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsFalse(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_UnityEngineは検出しないか確認()
        {
            // Arrange
            string code = "UnityEngine.Debug.Log(\"Hello World\");";
            
            // Act
            bool isDangerous = DynamicCodeSecurityManager.ContainsDangerousApi(code);
            
            // Assert
            Assert.IsFalse(isDangerous);
        }

        [Test]
        public void ContainsDangerousApi_空文字列でfalseを返すか確認()
        {
            // Act
            bool isDangerous1 = DynamicCodeSecurityManager.ContainsDangerousApi("");
            bool isDangerous2 = DynamicCodeSecurityManager.ContainsDangerousApi(null);
            bool isDangerous3 = DynamicCodeSecurityManager.ContainsDangerousApi("   ");
            
            // Assert
            Assert.IsFalse(isDangerous1);
            Assert.IsFalse(isDangerous2);
            Assert.IsFalse(isDangerous3);
        }

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

        [Test]
        public void IsCodeAllowedForCurrentLevel_Level0で常にfalseを返すか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Disabled);
            
            // Act
            bool allowed1 = DynamicCodeSecurityManager.IsCodeAllowedForCurrentLevel("GameObject.CreatePrimitive(PrimitiveType.Cube);");
            bool allowed2 = DynamicCodeSecurityManager.IsCodeAllowedForCurrentLevel("System.IO.File.ReadAllText(\"test.txt\");");
            
            // Assert
            Assert.IsFalse(allowed1);
            Assert.IsFalse(allowed2);
        }

        [Test]
        public void IsCodeAllowedForCurrentLevel_Level1で危険APIをブロックするか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            
            // Act
            bool allowedSafe = DynamicCodeSecurityManager.IsCodeAllowedForCurrentLevel("GameObject.CreatePrimitive(PrimitiveType.Cube);");
            bool allowedDangerous = DynamicCodeSecurityManager.IsCodeAllowedForCurrentLevel("System.IO.File.ReadAllText(\"test.txt\");");
            
            // Assert
            Assert.IsTrue(allowedSafe);
            Assert.IsFalse(allowedDangerous);
        }

        [Test]
        public void IsCodeAllowedForCurrentLevel_Level2で全て許可するか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
            
            // Act
            bool allowed1 = DynamicCodeSecurityManager.IsCodeAllowedForCurrentLevel("GameObject.CreatePrimitive(PrimitiveType.Cube);");
            bool allowed2 = DynamicCodeSecurityManager.IsCodeAllowedForCurrentLevel("System.IO.File.ReadAllText(\"test.txt\");");
            
            // Assert
            Assert.IsTrue(allowed1);
            Assert.IsTrue(allowed2);
        }

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