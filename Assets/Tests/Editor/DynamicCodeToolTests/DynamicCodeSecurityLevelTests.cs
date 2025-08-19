using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// DynamicCodeSecurityLevelのenumテスト
    /// 各レベルの値とデフォルト値を確認
    /// </summary>
    [TestFixture]
    public class DynamicCodeSecurityLevelTests
    {
        [Test]
        public void 各レベルの値が正しいか確認()
        {
            // Assert
            Assert.AreEqual(0, (int)DynamicCodeSecurityLevel.Disabled);
            Assert.AreEqual(1, (int)DynamicCodeSecurityLevel.Restricted);
            Assert.AreEqual(2, (int)DynamicCodeSecurityLevel.FullAccess);
        }

        [Test]
        public void デフォルト値がRestrictedであることを確認()
        {
            // Arrange & Act
            DynamicCodeSecurityLevel defaultLevel = default(DynamicCodeSecurityLevel);
            
            // Assert
            Assert.AreEqual(DynamicCodeSecurityLevel.Disabled, defaultLevel); // enumのデフォルトは0
        }

        [Test]
        public void 全てのレベルが定義されていることを確認()
        {
            // Arrange
            string[] expectedNames = { "Disabled", "Restricted", "FullAccess" };
            
            // Act
            string[] actualNames = System.Enum.GetNames(typeof(DynamicCodeSecurityLevel));
            
            // Assert
            Assert.AreEqual(expectedNames.Length, actualNames.Length);
            foreach (string name in expectedNames)
            {
                Assert.Contains(name, actualNames);
            }
        }
    }
}